using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using Xunit;

namespace MediaCleaner.Tests;

public class LeavingSoonCollectionServiceTests
{
    [Fact]
    public void ValidateCandidates_keeps_valid_items_and_reports_skips()
    {
        var validId = Guid.NewGuid();
        var missingId = Guid.NewGuid();
        var virtualId = Guid.NewGuid();
        var valid = NewMovie(validId, "Valid", "/media/valid.mkv");
        var duplicate = NewMovie(validId, "Duplicate", "/media/duplicate.mkv");
        var missing = NewMovie(missingId, "Missing", "/media/missing.mkv");
        var virtualMovie = NewMovie(virtualId, "Virtual", "/media/virtual.mkv");
        virtualMovie.IsVirtualItem = true;

        var result = LeavingSoonCollectionService.ValidateCandidates(
            [valid, duplicate, missing, virtualMovie],
            id => id == validId ? valid : id == virtualId ? virtualMovie : null);

        Assert.Equal([validId], result.ValidIds);
        Assert.Equal(3, result.Skipped.Count);
        Assert.Contains(result.Skipped, item => item.Id == validId && item.Reason.Contains("Duplicate", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Skipped, item => item.Id == missingId && item.Reason.Contains("not found", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Skipped, item => item.Id == virtualId && item.Reason.Contains("virtual", StringComparison.OrdinalIgnoreCase));
    }

    private static Movie NewMovie(Guid id, string name, string path) =>
        new()
        {
            Id = id,
            Name = name,
            Path = path
        };
}
