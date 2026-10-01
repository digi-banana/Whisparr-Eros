using System;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.MediaFiles.MovieImport;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles.MovieImport
{
    [TestFixture]
    public class DetectSampleFixture : CoreTest<DetectSample>
    {
        private MovieMetadata _movie;
        private LocalMovie _localMovie;

        [SetUp]
        public void Setup()
        {
            _movie = Builder<MovieMetadata>.CreateNew()
                                     .With(s => s.Runtime = 30)
                                     .With(s => s.ItemType = ItemType.Movie)
                                     .Build();

            _localMovie = new LocalMovie
            {
                Path = @"C:\Test\30 Rock\30.rock.s01e01.avi",
                Movie = new Movie { MovieMetadata = _movie },
                Quality = new QualityModel(Quality.HDTV720p)
            };
        }

        private void GivenRuntime(int seconds)
        {
            Mocker.GetMock<IVideoFileInfoReader>()
                  .Setup(s => s.GetRunTime(It.IsAny<string>()))
                  .Returns(new TimeSpan(0, 0, seconds));
        }

        [Test]
        public void should_return_false_for_flv()
        {
            _localMovie.Path = @"C:\Test\some.show.s01e01.flv";

            ShouldBeNotSample();

            Mocker.GetMock<IVideoFileInfoReader>().Verify(c => c.GetRunTime(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_return_false_for_strm()
        {
            _localMovie.Path = @"C:\Test\some.show.s01e01.strm";

            ShouldBeNotSample();

            Mocker.GetMock<IVideoFileInfoReader>().Verify(c => c.GetRunTime(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_return_false_for_iso()
        {
            _localMovie.Path = @"C:\Test\some movie (2000).iso";

            ShouldBeNotSample();

            Mocker.GetMock<IVideoFileInfoReader>().Verify(c => c.GetRunTime(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_return_false_for_img()
        {
            _localMovie.Path = @"C:\Test\some movie (2000).img";

            ShouldBeNotSample();

            Mocker.GetMock<IVideoFileInfoReader>().Verify(c => c.GetRunTime(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_return_false_for_m2ts()
        {
            _localMovie.Path = @"C:\Test\some movie (2000).m2ts";

            ShouldBeNotSample();

            Mocker.GetMock<IVideoFileInfoReader>().Verify(c => c.GetRunTime(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_use_runtime()
        {
            GivenRuntime(120);

            Subject.IsSample(_localMovie.Movie.MovieMetadata,
                             _localMovie.Path);

            Mocker.GetMock<IVideoFileInfoReader>().Verify(v => v.GetRunTime(It.IsAny<string>()), Times.Once());
        }

        [Test]
        public void should_return_true_if_runtime_is_less_than_minimum()
        {
            GivenRuntime(60);

            ShouldBeSample();
        }

        [Test]
        public void should_return_false_if_runtime_greater_than_minimum()
        {
            GivenRuntime(600);

            ShouldBeNotSample();
        }

        [Test]
        public void should_return_false_if_runtime_greater_than_webisode_minimum()
        {
            _movie.Runtime = 6;
            GivenRuntime(299);

            ShouldBeNotSample();
        }

        [Test]
        public void should_return_false_if_runtime_greater_than_anime_short_minimum()
        {
            _movie.Runtime = 2;
            GivenRuntime(60);

            ShouldBeNotSample();
        }

        [Test]
        public void should_return_true_if_runtime_less_than_anime_short_minimum()
        {
            _movie.Runtime = 2;
            GivenRuntime(10);

            ShouldBeSample();
        }

        [Test]
        public void should_return_indeterminate_if_mediainfo_result_is_null()
        {
            Mocker.GetMock<IVideoFileInfoReader>()
                  .Setup(s => s.GetRunTime(It.IsAny<string>()))
                  .Returns((TimeSpan?)null);

            Subject.IsSample(_localMovie.Movie.MovieMetadata,
                             _localMovie.Path).Should().Be(DetectSampleResult.Indeterminate);

            ExceptionVerification.ExpectedErrors(1);
        }

        private void GivenScene(int runtimeMinutes, string path, long size)
        {
            _movie.ItemType = ItemType.Scene;
            _movie.Runtime = runtimeMinutes;
            _localMovie.Path = path;
            _localMovie.Size = size;
        }

        private void GivenNoRuntime()
        {
            Mocker.GetMock<IVideoFileInfoReader>()
                  .Setup(s => s.GetRunTime(It.IsAny<string>()))
                  .Returns((TimeSpan?)null);
        }

        [Test]
        public void should_return_not_sample_for_scene_with_unknown_expected_runtime_and_full_length_file()
        {
            GivenScene(0, "/downloads/Studio - Scene Title 1080p/Studio - Scene Title 1080p.mp4", 650.Megabytes());
            GivenRuntime(25 * 60);

            Subject.IsSample(_localMovie).Should().Be(DetectSampleResult.NotSample);
        }

        [Test]
        public void should_use_media_info_from_import_for_scene()
        {
            GivenScene(0, "/downloads/Studio - Scene Title 1080p/Studio - Scene Title 1080p.mp4", 650.Megabytes());
            _localMovie.MediaInfo = new MediaInfoModel { RunTime = TimeSpan.FromMinutes(25) };

            Subject.IsSample(_localMovie).Should().Be(DetectSampleResult.NotSample);

            Mocker.GetMock<IVideoFileInfoReader>().Verify(v => v.GetRunTime(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_read_runtime_for_scene_if_media_info_is_missing()
        {
            GivenScene(30, "/downloads/Studio - Scene Title 1080p/Studio - Scene Title 1080p.mp4", 650.Megabytes());
            GivenRuntime(25 * 60);

            Subject.IsSample(_localMovie).Should().Be(DetectSampleResult.NotSample);

            Mocker.GetMock<IVideoFileInfoReader>().Verify(v => v.GetRunTime(It.IsAny<string>()), Times.Once());
        }

        [TestCase(0)]
        [TestCase(30)]
        public void should_return_not_sample_for_large_scene_file_if_runtime_cannot_be_read(int expectedRuntime)
        {
            GivenScene(expectedRuntime, "/mnt/debrid/whisparr/Studio - Scene Title 1080p/Studio - Scene Title 1080p.mp4", 650.Megabytes());
            GivenNoRuntime();

            Subject.IsSample(_localMovie).Should().Be(DetectSampleResult.NotSample);

            ExceptionVerification.ExpectedErrors(1);
            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public void should_return_not_sample_for_scene_with_examples_in_name_if_runtime_cannot_be_read()
        {
            GivenScene(0, "/downloads/Studio - Kinky Examples/Studio - Kinky Examples.mp4", 650.Megabytes());
            GivenNoRuntime();

            Subject.IsSample(_localMovie).Should().Be(DetectSampleResult.NotSample);

            ExceptionVerification.ExpectedErrors(1);
            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public void should_return_indeterminate_for_small_scene_file_if_runtime_cannot_be_read()
        {
            GivenScene(0, "/downloads/Studio - Scene Title 1080p/Studio - Scene Title 1080p.mp4", 40.Megabytes());
            GivenNoRuntime();

            Subject.IsSample(_localMovie).Should().Be(DetectSampleResult.Indeterminate);

            ExceptionVerification.ExpectedErrors(1);
        }

        [TestCase("/downloads/Studio - Scene Title 1080p/studio-scene-title-1080p-sample.mp4")]
        [TestCase("/downloads/Studio - Scene Title 1080p/Sample/studio-scene-title-1080p.mp4")]
        [TestCase("/downloads/Studio - Scene Title 1080p/Samples/studio-scene-title-1080p.mp4")]
        [TestCase("/downloads/Studio - Scene Title 1080p/Studio.Scene.Title.1080p.SAMPLE.mkv")]
        public void should_return_indeterminate_for_scene_named_as_sample_if_runtime_cannot_be_read(string path)
        {
            GivenScene(0, path, 650.Megabytes());
            GivenNoRuntime();

            Subject.IsSample(_localMovie).Should().Be(DetectSampleResult.Indeterminate);

            ExceptionVerification.ExpectedErrors(1);
        }

        [TestCase(0, 10)]
        [TestCase(30, 60)]
        public void should_return_sample_for_short_scene_file(int expectedRuntime, int fileRuntimeSeconds)
        {
            GivenScene(expectedRuntime, "/downloads/Studio - Scene Title 1080p/Studio - Scene Title 1080p.mp4", 650.Megabytes());
            GivenRuntime(fileRuntimeSeconds);

            Subject.IsSample(_localMovie).Should().Be(DetectSampleResult.Sample);
        }

        [Test]
        public void should_return_sample_for_scene_with_zero_runtime_media_info()
        {
            GivenScene(0, "/downloads/Studio - Scene Title 1080p/Studio - Scene Title 1080p.mp4", 650.Megabytes());
            _localMovie.MediaInfo = new MediaInfoModel { RunTime = TimeSpan.Zero };

            Subject.IsSample(_localMovie).Should().Be(DetectSampleResult.Sample);

            ExceptionVerification.ExpectedErrors(1);
        }

        [Test]
        public void should_return_indeterminate_for_large_movie_file_if_runtime_cannot_be_read()
        {
            _localMovie.Size = 4000.Megabytes();
            GivenNoRuntime();

            Subject.IsSample(_localMovie).Should().Be(DetectSampleResult.Indeterminate);

            ExceptionVerification.ExpectedErrors(1);
        }

        [Test]
        public void should_not_use_media_info_from_import_for_movie()
        {
            _localMovie.MediaInfo = new MediaInfoModel { RunTime = TimeSpan.FromMinutes(90) };
            GivenRuntime(60);

            Subject.IsSample(_localMovie).Should().Be(DetectSampleResult.Sample);

            Mocker.GetMock<IVideoFileInfoReader>().Verify(v => v.GetRunTime(It.IsAny<string>()), Times.Once());
        }

        private void ShouldBeSample()
        {
            Subject.IsSample(_localMovie.Movie.MovieMetadata,
                             _localMovie.Path).Should().Be(DetectSampleResult.Sample);
        }

        private void ShouldBeNotSample()
        {
            Subject.IsSample(_localMovie.Movie.MovieMetadata,
                             _localMovie.Path).Should().Be(DetectSampleResult.NotSample);
        }
    }
}
