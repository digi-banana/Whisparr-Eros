namespace NzbDrone.Core.Movies
{
    public enum MovieParseMatchType
    {
        StashId = 1,
        Title = 2,
        Episode = 3,
        PerformersTitle = 4,
        CharactersTitle = 5,
        Performers = 6,
        Characters = 7,
        PerformerTitle = 8,
        CharacterTitle = 9,
        PerformersNotTitle = 10,
        CharactersNotTitle = 11,
        ParsedTitleContainsCleanTitle = 12,

        // Release without a date naming exactly the scene's performers (aliases included), see DatelessSceneEvidence
        PerformersExact = 13,

        // Release without a date that has the title and a performer, but strict name matching can't tell them apart
        // (one is part of the other, or a one-word name is part of a longer one), see MovieService.ApplyStrictNameMatching
        PerformerTitleUnconfirmed = 14
    }
}
