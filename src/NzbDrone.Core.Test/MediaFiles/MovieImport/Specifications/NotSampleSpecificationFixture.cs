using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles.MovieImport;
using NzbDrone.Core.MediaFiles.MovieImport.Specifications;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MediaFiles.MovieImport.Specifications
{
    [TestFixture]
    public class NotSampleSpecificationFixture : CoreTest<NotSampleSpecification>
    {
        private Movie _movie;
        private LocalMovie _localEpisode;

        [SetUp]
        public void Setup()
        {
            _movie = Builder<Movie>.CreateNew()
                                     .Build();

            _localEpisode = new LocalMovie
            {
                Path = @"C:\Test\30 Rock\30.rock.s01e01.avi",
                Movie = _movie,
            };
        }

        private void GivenDetectSampleResult(DetectSampleResult result)
        {
            Mocker.GetMock<IDetectSample>()
                  .Setup(s => s.IsSample(It.IsAny<LocalMovie>()))
                  .Returns(result);
        }

        [Test]
        public void should_return_true_for_existing_file()
        {
            _localEpisode.ExistingFile = true;
            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_accept_when_not_a_sample()
        {
            GivenDetectSampleResult(DetectSampleResult.NotSample);

            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeTrue();

            Mocker.GetMock<IDetectSample>().Verify(v => v.IsSample(_localEpisode), Times.Once());
        }

        [Test]
        public void should_reject_sample()
        {
            GivenDetectSampleResult(DetectSampleResult.Sample);

            var result = Subject.IsSatisfiedBy(_localEpisode, null);

            result.Accepted.Should().BeFalse();
            result.Reason.Should().Be(ImportRejectionReason.Sample);
        }

        [Test]
        public void should_reject_when_sample_status_is_indeterminate()
        {
            GivenDetectSampleResult(DetectSampleResult.Indeterminate);

            var result = Subject.IsSatisfiedBy(_localEpisode, null);

            result.Accepted.Should().BeFalse();
            result.Reason.Should().Be(ImportRejectionReason.SampleIndeterminate);
        }
    }
}
