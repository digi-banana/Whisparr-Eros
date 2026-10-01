using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Notifications.Stash;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.NotificationTests.StashTests
{
    [TestFixture]
    public class StashServiceFixture : CoreTest<StashService>
    {
        private StashSettings _settings;
        private Movie _movie;

        [SetUp]
        public void Setup()
        {
            _settings = new StashSettings
            {
                Host = "localhost",
                Port = 5555
            };

            _movie = new Movie
            {
                Path = "/data/media/scenes/Studio/2024-01-01 - Scene Title"
            };

            Mocker.GetMock<ILocalizationService>()
                  .Setup(s => s.GetLocalizedString(It.IsAny<string>()))
                  .Returns<string>(s => s);

            Mocker.GetMock<ILocalizationService>()
                  .Setup(s => s.GetLocalizedString(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
                  .Returns<string, Dictionary<string, object>>((s, _) => s);
        }

        private void GivenStatus(string status)
        {
            Mocker.GetMock<IStashProxy>()
                  .Setup(s => s.GetStatus(_settings))
                  .Returns(new StashStatusData { SystemStatus = new StashSystemStatus { Status = status } });
        }

        private void GivenStatusThrows(Exception ex)
        {
            Mocker.GetMock<IStashProxy>()
                  .Setup(s => s.GetStatus(_settings))
                  .Throws(ex);
        }

        private static HttpException GivenHttpException(HttpStatusCode statusCode)
        {
            var request = new HttpRequest("http://localhost:5555/graphql");

            return new HttpException(new HttpResponse(request, new HttpHeader(), Array.Empty<byte>(), statusCode));
        }

        [TestCase("/data/media", "/stash/library", "/stash/library/scenes/Studio/2024-01-01 - Scene Title")]
        [TestCase("/data/media/", "/stash/library/", "/stash/library/scenes/Studio/2024-01-01 - Scene Title")]
        [TestCase("/data/media/scenes/Studio/2024-01-01 - Scene Title", "/stash", "/stash")]
        [TestCase("/data/media", "D:\\Library", "D:\\Library\\scenes\\Studio\\2024-01-01 - Scene Title")]
        [TestCase("/other", "/stash/library", "/data/media/scenes/Studio/2024-01-01 - Scene Title")]
        [TestCase("", "", "/data/media/scenes/Studio/2024-01-01 - Scene Title")]
        public void should_map_path(string mapFrom, string mapTo, string expected)
        {
            _settings.MapFrom = mapFrom;
            _settings.MapTo = mapTo;

            Subject.MapPath(_settings, _movie.Path).Should().Be(expected);
        }

        [Test]
        public void update_should_scan_mapped_scene_folder()
        {
            _settings.MapFrom = "/data/media";
            _settings.MapTo = "/stash/library";

            Subject.Update(_settings, _movie);

            Mocker.GetMock<IStashProxy>().Verify(v => v.Scan(_settings, "/stash/library/scenes/Studio/2024-01-01 - Scene Title"), Times.Once());
            Mocker.GetMock<IStashProxy>().Verify(v => v.Identify(It.IsAny<StashSettings>(), It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void update_should_identify_when_enabled()
        {
            _settings.MetadataIdentify = true;

            Subject.Update(_settings, _movie);

            Mocker.GetMock<IStashProxy>().Verify(v => v.Scan(_settings, _movie.Path), Times.Once());
            Mocker.GetMock<IStashProxy>().Verify(v => v.Identify(_settings, _movie.Path), Times.Once());
        }

        [Test]
        public void clean_should_clean_scene_folder()
        {
            Subject.Clean(_settings, _movie);

            Mocker.GetMock<IStashProxy>().Verify(v => v.Clean(_settings, _movie.Path), Times.Once());
        }

        [Test]
        public void test_should_pass_when_status_is_ok()
        {
            GivenStatus("OK");

            Subject.Test(_settings).Should().BeNull();
        }

        [TestCase("SETUP")]
        [TestCase("NEEDS_MIGRATION")]
        [TestCase(null)]
        public void test_should_fail_when_stash_is_not_ready(string status)
        {
            GivenStatus(status);

            var result = Subject.Test(_settings);

            result.PropertyName.Should().Be("Host");
            result.ErrorMessage.Should().Be("NotificationsStashValidationNotReady");
        }

        [Test]
        public void test_should_report_invalid_api_key_on_unauthorized()
        {
            GivenStatusThrows(GivenHttpException(HttpStatusCode.Unauthorized));

            var result = Subject.Test(_settings);

            result.PropertyName.Should().Be("ApiKey");
            result.ErrorMessage.Should().Be("NotificationsValidationInvalidApiKey");
            ExceptionVerification.ExpectedErrors(1);
        }

        [Test]
        public void test_should_report_forbidden()
        {
            GivenStatusThrows(GivenHttpException(HttpStatusCode.Forbidden));

            var result = Subject.Test(_settings);

            result.PropertyName.Should().Be("ApiKey");
            result.ErrorMessage.Should().Be("NotificationsStashValidationForbidden");
            ExceptionVerification.ExpectedErrors(1);
        }

        [Test]
        public void test_should_report_other_http_errors()
        {
            GivenStatusThrows(GivenHttpException(HttpStatusCode.NotFound));

            var result = Subject.Test(_settings);

            result.PropertyName.Should().Be("Host");
            result.ErrorMessage.Should().Be("NotificationsValidationUnableToConnectToApi");
            ExceptionVerification.ExpectedErrors(1);
        }

        [Test]
        public void test_should_report_unreachable_host()
        {
            GivenStatusThrows(new HttpRequestException("Connection refused"));

            var result = Subject.Test(_settings);

            result.PropertyName.Should().Be("Host");
            result.ErrorMessage.Should().Be("NotificationsValidationUnableToConnect");
            ExceptionVerification.ExpectedErrors(1);
        }

        [Test]
        public void test_should_report_graphql_errors()
        {
            GivenStatusThrows(new StashException("Stash returned an error: boom"));

            var result = Subject.Test(_settings);

            result.PropertyName.Should().Be("Host");
            result.ErrorMessage.Should().Be("NotificationsValidationUnableToSendTestMessageApiResponse");
            ExceptionVerification.ExpectedErrors(1);
        }
    }
}
