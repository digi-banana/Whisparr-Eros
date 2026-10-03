using System.Collections.Generic;
using System.Collections.Specialized;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Processes;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Notifications.CustomScript;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tags;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.NotificationTests.CustomScriptTests
{
    [TestFixture]
    public class CustomScriptFixture : CoreTest<CustomScript>
    {
        private const string SourcePath = "/downloads/seed/Helix - Tight Package (Max Carter, Ezra Michaels).mp4";

        private StringDictionary _environment;

        [SetUp]
        public void Setup()
        {
            Subject.Definition = new NotificationDefinition { Settings = new CustomScriptSettings { Path = "/scripts/seed.sh" } };

            Mocker.GetMock<IProcessProvider>()
                  .Setup(s => s.StartAndCapture(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<StringDictionary>()))
                  .Callback<string, string, StringDictionary>((_, _, environment) => _environment = environment)
                  .Returns(new ProcessOutput());

            Mocker.GetMock<ITagRepository>()
                  .Setup(s => s.GetTags(It.IsAny<HashSet<int>>()))
                  .Returns(new List<Tag>());
        }

        private DownloadMessage GivenDownload(MediaInfoModel mediaInfo)
        {
            var movie = new Movie { Id = 1294, Tags = new HashSet<int>(), Path = "/scenes/Helix Studios/2017-12-17 - Tight Package" };

            movie.MovieMetadata.Value.Title = "Tight Package";
            movie.MovieMetadata.Value.ForeignId = "da67da88-3076-4e70-bd74-37ee13f3d240";
            movie.MovieMetadata.Value.OriginalLanguage = Language.English;

            var movieFile = new MovieFile
            {
                Id = 7,
                RelativePath = "Helix - Tight Package (Max Carter, Ezra Michaels).mp4",
                Quality = new QualityModel(Quality.WEBDL720p),
                MediaInfo = mediaInfo
            };

            return new DownloadMessage
            {
                Movie = movie,
                MovieFile = movieFile,
                MovieInfo = new LocalMovie { Path = SourcePath },
                OldMovieFiles = new List<DeletedMovieFile>(),
                SourcePath = SourcePath
            };
        }

        [Test]
        public void should_run_script_on_download_of_a_file_without_media_info()
        {
            Subject.OnDownload(GivenDownload(null));

            _environment.Should().NotBeNull();
            _environment["Whisparr_EventType"].Should().Be("Download");
            _environment["Whisparr_MovieFile_SourcePath"].Should().Be(SourcePath);
            _environment["Whisparr_MovieFile_Path"].Should().EndWith("Helix - Tight Package (Max Carter, Ezra Michaels).mp4");
            _environment["Whisparr_MovieFile_MediaInfo_Height"].Should().BeEmpty();
            _environment["Whisparr_MovieFile_MediaInfo_AudioLanguages"].Should().BeEmpty();
        }

        [Test]
        public void should_pass_media_info_when_known()
        {
            Subject.OnDownload(GivenDownload(new MediaInfoModel { Height = 720, Width = 1280, AudioLanguages = new List<string> { "eng" }, Subtitles = new List<string>() }));

            _environment["Whisparr_MovieFile_MediaInfo_Height"].Should().Be("720");
            _environment["Whisparr_MovieFile_MediaInfo_Width"].Should().Be("1280");
            _environment["Whisparr_MovieFile_MediaInfo_AudioLanguages"].Should().Be("eng");
        }
    }
}
