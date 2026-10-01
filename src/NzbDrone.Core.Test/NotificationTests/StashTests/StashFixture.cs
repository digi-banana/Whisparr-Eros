using Moq;
using NUnit.Framework;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Notifications.Stash;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.NotificationTests.StashTests
{
    [TestFixture]
    public class StashFixture : CoreTest<Stash>
    {
        private Movie _movie;

        [SetUp]
        public void Setup()
        {
            _movie = new Movie { Path = "/data/media/scenes/Studio/Scene" };

            Subject.Definition = new NotificationDefinition
            {
                Settings = new StashSettings { Host = "localhost", Port = 9999 }
            };
        }

        [Test]
        public void should_scan_on_download()
        {
            Subject.OnDownload(new DownloadMessage { Movie = _movie });

            Mocker.GetMock<IStashService>().Verify(v => v.Update(It.IsAny<StashSettings>(), _movie), Times.Once());
        }

        [Test]
        public void should_clean_on_movie_file_delete()
        {
            Subject.OnMovieFileDelete(new MovieFileDeleteMessage { Movie = _movie });

            Mocker.GetMock<IStashService>().Verify(v => v.Clean(It.IsAny<StashSettings>(), _movie), Times.Once());
        }

        [TestCase(true, 1)]
        [TestCase(false, 0)]
        public void should_clean_on_movie_delete_only_when_files_were_deleted(bool deletedFiles, int times)
        {
            Subject.OnMovieDelete(new MovieDeleteMessage(_movie, deletedFiles));

            Mocker.GetMock<IStashService>().Verify(v => v.Clean(It.IsAny<StashSettings>(), _movie), Times.Exactly(times));
        }
    }
}
