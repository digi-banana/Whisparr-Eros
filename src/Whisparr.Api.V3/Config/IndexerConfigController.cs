using FluentValidation;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.IndexerSearch;
using Whisparr.Http;
using Whisparr.Http.Validation;

namespace Whisparr.Api.V3.Config
{
    [V3ApiController("config/indexer")]
    public class IndexerConfigController : ConfigController<IndexerConfigResource>
    {
        public IndexerConfigController(IConfigService configService)
            : base(configService)
        {
            SharedValidator.RuleFor(c => c.MinimumAge)
                           .GreaterThanOrEqualTo(0);

            SharedValidator.RuleFor(c => c.MaximumSize)
                           .GreaterThanOrEqualTo(0);

            SharedValidator.RuleFor(c => c.Retention)
                           .GreaterThanOrEqualTo(0);

            SharedValidator.RuleFor(c => c.RssSyncInterval)
                           .IsValidRssSyncInterval();

            SharedValidator.RuleFor(c => c.PacedMissingSearchItemsPerRun)
                           .InclusiveBetween(1, PacedMissingSearchService.MaximumItemsPerRun);

            SharedValidator.RuleFor(c => c.PacedMissingSearchInterval)
                           .GreaterThanOrEqualTo(PacedMissingSearchService.MinimumInterval);
        }

        protected override IndexerConfigResource ToResource(IConfigService model)
        {
            return IndexerConfigResourceMapper.ToResource(model);
        }
    }
}
