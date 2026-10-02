using System;
using System.Collections.Generic;

namespace NzbDrone.Core.IndexerSearch.Definitions
{
    public class SceneSearchCriteria : SearchCriteriaBase
    {
        public string SiteTitle { get; set; }
        public DateOnly? ReleaseDate { get; set; }
        public string Performer { get; set; }

        // The scene's titles alone (title and alternative titles), for indexers set to search scenes by title only
        public List<string> TitleOnlySceneTitles { get; set; } = new ();

        public override string ToString()
        {
            return string.Format("[{0}]", Movie.Title);
        }
    }
}
