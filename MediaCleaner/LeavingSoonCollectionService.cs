using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace MediaCleaner;

internal sealed record LeavingSoonCandidateValidationResult(
    IReadOnlyList<Guid> ValidIds,
    IReadOnlyList<LeavingSoonSkippedCandidate> Skipped);

internal sealed record LeavingSoonSkippedCandidate(
    Guid Id,
    string Name,
    string? Path,
    string Reason);

internal class LeavingSoonCollectionService(ILogger<LeavingSoonCollectionService> logger, ILibraryManager libraryManager, ICollectionManager collectionManager)
{
    internal const string CollectionName = "Leaving Soon";
    private const string Tag = "Media Cleaner";

    public async Task Finish(IEnumerable<BaseItem> candidates)
    {
        try
        {
            var rawCandidates = candidates.ToList();
            var validation = ValidateCandidates(rawCandidates, libraryManager.GetItemById);

            logger.LogInformation(
                "Finishing collection {CollectionName} with {ItemCount} raw candidate item(s).",
                CollectionName,
                rawCandidates.Count);
            logger.LogInformation(
                "Leaving Soon candidate validation: {RawCount} raw, {ValidCount} valid, {InvalidCount} invalid, {SkippedCount} skipped.",
                rawCandidates.Count,
                validation.ValidIds.Count,
                validation.Skipped.Count,
                validation.Skipped.Count);

            foreach (var skipped in validation.Skipped)
            {
                logger.LogWarning(
                    "Skipping Leaving Soon candidate \"{Name}\" ({Id}) at path \"{Path}\": {Reason}",
                    skipped.Name,
                    skipped.Id,
                    skipped.Path ?? "[unknown]",
                    skipped.Reason);
            }

            var collection = await GetBoxSetByName(CollectionName, validation.ValidIds.Count > 0);

            var removedCount = 0;
            if (collection is not null)
            {
                var query = new InternalItemsQuery { CollapseBoxSetItems = false, Recursive = true, Parent = collection };
                var items = collection.GetItems(query).Items.Select(b => b.Id).ToList();
                removedCount = items.Count;

                logger.LogInformation(
                    "Removing {ItemCount} existing item(s) from collection {CollectionName}.",
                    items.Count,
                    CollectionName);
                await collectionManager.RemoveFromCollectionAsync(collection.Id, items);
            }
            else
            {
                logger.LogInformation(
                    "Collection {CollectionName} was not found and no candidate items require it to be created.",
                    CollectionName);
            }

            var addedCount = 0;
            if (validation.ValidIds.Count > 0 && collection is not null)
            {
                logger.LogInformation(
                    "Adding {ItemCount} item(s) to collection {CollectionName}.",
                    validation.ValidIds.Count,
                    CollectionName);
                addedCount = await AddToCollectionAsync(collection, validation.ValidIds, rawCandidates);
                await SetPhotoForCollection(collection);
            }

            var finalCount = collection is null
                ? 0
                : collection.GetItems(new InternalItemsQuery { CollapseBoxSetItems = false, Recursive = true, Parent = collection }).Items.Count;
            logger.LogInformation(
                "Leaving Soon collection update finished. Removed: {RemovedCount}. Added: {AddedCount}. Final collection count: {FinalCount}.",
                removedCount,
                addedCount,
                finalCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error finishing collection {CollectionName}", CollectionName);
        }
    }

    internal static LeavingSoonCandidateValidationResult ValidateCandidates(
        IEnumerable<BaseItem> candidates,
        Func<Guid, BaseItem?> resolveItem)
    {
        var validIds = new List<Guid>();
        var skipped = new List<LeavingSoonSkippedCandidate>();
        var seen = new HashSet<Guid>();

        foreach (var candidate in candidates)
        {
            if (!seen.Add(candidate.Id))
            {
                skipped.Add(ToSkipped(candidate, "Duplicate candidate ID."));
                continue;
            }

            BaseItem? resolved;
            try
            {
                resolved = resolveItem(candidate.Id);
            }
            catch (Exception ex)
            {
                skipped.Add(ToSkipped(candidate, $"Library lookup failed: {ex.Message}"));
                continue;
            }

            if (resolved is null)
            {
                skipped.Add(ToSkipped(candidate, "Item not found in Jellyfin library."));
                continue;
            }

            if (resolved.IsVirtualItem)
            {
                skipped.Add(ToSkipped(candidate, "Item is virtual."));
                continue;
            }

            validIds.Add(candidate.Id);
        }

        return new LeavingSoonCandidateValidationResult(validIds, skipped);
    }

    internal static BoxSet? GetExistingCollection(ILibraryManager libraryManager) =>
        FindBoxSetByName(libraryManager, CollectionName);

    private async Task<BoxSet?> GetBoxSetByName(string name, bool create)
    {
        var collection = FindBoxSetByName(libraryManager, name);

        if (collection is null && create)
        {
            logger.LogInformation("{Name} not found, creating.", name);
            collection = await collectionManager.CreateCollectionAsync(new CollectionCreationOptions { Name = name, IsLocked = false });
            collection.Tags = [Tag];
            await libraryManager.UpdateItemAsync(
                collection,
                collection.GetParent(),
                ItemUpdateType.MetadataEdit,
                CancellationToken.None);
            logger.LogInformation("{Name} collection created with id {Id}.", name, collection.Id);
        }
        else if (collection is not null)
        {
            logger.LogInformation("{Name} collection found with id {Id}.", name, collection.Id);
        }

        return collection;
    }

    private static BoxSet? FindBoxSetByName(ILibraryManager libraryManager, string name) =>
        libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.BoxSet],
            CollapseBoxSetItems = false,
            Recursive = true,
            Tags = [Tag],
            Name = name,
        }).OfType<BoxSet>().FirstOrDefault();

    private async Task<int> AddToCollectionAsync(BoxSet collection, IReadOnlyList<Guid> ids, IReadOnlyList<BaseItem> candidates)
    {
        try
        {
            await collectionManager.AddToCollectionAsync(collection.Id, ids);
            return ids.Count;
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(
                ex,
                "Batch add to collection {CollectionName} failed; retrying {ItemCount} item(s) individually.",
                CollectionName,
                ids.Count);
        }

        var addedCount = 0;
        var candidateById = candidates.GroupBy(item => item.Id).ToDictionary(group => group.Key, group => group.First());
        foreach (var id in ids)
        {
            try
            {
                await collectionManager.AddToCollectionAsync(collection.Id, [id]);
                addedCount++;
            }
            catch (Exception ex)
            {
                candidateById.TryGetValue(id, out var candidate);
                var skipped = candidate is null
                    ? new LeavingSoonSkippedCandidate(id, "[unknown]", null, ex.Message)
                    : ToSkipped(candidate, ex.Message);
                logger.LogWarning(
                    ex,
                    "Skipping Leaving Soon candidate \"{Name}\" ({Id}) at path \"{Path}\": {Reason}",
                    skipped.Name,
                    skipped.Id,
                    skipped.Path ?? "[unknown]",
                    skipped.Reason);
            }
        }

        return addedCount;
    }

    private static LeavingSoonSkippedCandidate ToSkipped(BaseItem item, string reason) =>
        new(
            item.Id,
            item.Name ?? "[unknown]",
            string.IsNullOrWhiteSpace(item.Path) ? null : item.Path,
            reason);

    private async Task SetPhotoForCollection(BoxSet collection)
    {
        try
        {
            var query = new InternalItemsQuery { Recursive = true };

            var mediaItemWithImage = collection.GetItems(query)
                .Items
                .Where(item => item is Movie or Series or Season or Video)
                .FirstOrDefault(item =>
                    item.ImageInfos != null &&
                    item.ImageInfos.Any(i => i.Type == ImageType.Primary));

            if (mediaItemWithImage != null)
            {
                var imageInfo = mediaItemWithImage.ImageInfos
                    .First(i => i.Type == ImageType.Primary);

                collection.SetImage(new ItemImageInfo { Path = imageInfo.Path, Type = ImageType.Primary }, 0);

                await libraryManager.UpdateItemAsync(
                    collection,
                    collection.GetParent(),
                    ItemUpdateType.ImageUpdate,
                    CancellationToken.None);

                logger.LogTrace("Successfully set image for collection {CollectionName} from {ItemName}", collection.Name, mediaItemWithImage.Name);
            }
            else
            {
                logger.LogTrace("No items with images found in collection {CollectionName}", collection.Name);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting image for collection {CollectionName}", collection.Name);
        }
    }
}
