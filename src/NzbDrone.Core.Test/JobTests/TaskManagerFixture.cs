using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Cache;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Configuration.Events;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Jobs;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.JobTests
{
    [TestFixture]
    public class TaskManagerFixture : CoreTest<TaskManager>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.SetConstant<ICacheManager>(new CacheManager());

            Mocker.GetMock<IScheduledTaskRepository>()
                  .Setup(s => s.All())
                  .Returns(new List<ScheduledTask>());

            Mocker.GetMock<IScheduledTaskRepository>()
                  .Setup(s => s.GetDefinition(It.IsAny<Type>()))
                  .Returns<Type>(t => new ScheduledTask { TypeName = t.FullName });
        }

        private void GivenPacedMissingSearch(bool enabled, int interval)
        {
            Mocker.GetMock<IConfigService>().SetupGet(s => s.PacedMissingSearchEnabled).Returns(enabled);
            Mocker.GetMock<IConfigService>().SetupGet(s => s.PacedMissingSearchInterval).Returns(interval);
        }

        private ScheduledTask GetPacedMissingSearchTask()
        {
            return Subject.GetAll().Single(t => t.TypeName == typeof(PacedMissingSearchCommand).FullName);
        }

        [Test]
        public void should_register_paced_missing_search_as_disabled_by_default()
        {
            GivenPacedMissingSearch(false, 60);

            Subject.Handle(new ApplicationStartedEvent());

            GetPacedMissingSearchTask().Interval.Should().Be(0);
            Subject.GetPending().Should().NotContain(t => t.TypeName == typeof(PacedMissingSearchCommand).FullName);
        }

        [Test]
        public void should_register_paced_missing_search_with_configured_interval()
        {
            GivenPacedMissingSearch(true, 90);

            Subject.Handle(new ApplicationStartedEvent());

            GetPacedMissingSearchTask().Interval.Should().Be(90);
        }

        [Test]
        public void should_update_paced_missing_search_interval_when_config_is_saved()
        {
            GivenPacedMissingSearch(false, 60);
            Subject.Handle(new ApplicationStartedEvent());

            GivenPacedMissingSearch(true, 30);
            Subject.HandleAsync(new ConfigSavedEvent());

            GetPacedMissingSearchTask().Interval.Should().Be(30);

            Mocker.GetMock<IScheduledTaskRepository>()
                  .Verify(v => v.UpdateMany(It.Is<IList<ScheduledTask>>(l => l.Any(t => t.TypeName == typeof(PacedMissingSearchCommand).FullName && t.Interval == 30))), Times.Once());
        }
    }
}
