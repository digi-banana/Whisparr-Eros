using FluentValidation;
using Newtonsoft.Json;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Notifications.Stash
{
    public class StashSettingsValidator : AbstractValidator<StashSettings>
    {
        public StashSettingsValidator()
        {
            RuleFor(c => c.Host).ValidHost();
            RuleFor(c => c.Port).ValidPort();
            RuleFor(c => c.UrlBase).ValidUrlBase();
            RuleFor(c => c.StashBoxEndpoint).IsValidUrl().When(c => c.MetadataIdentify && c.StashBoxEndpoint.IsNotNullOrWhiteSpace());
            RuleFor(c => c.MapFrom).NotEmpty().Unless(c => c.MapTo.IsNullOrWhiteSpace());
            RuleFor(c => c.MapTo).NotEmpty().Unless(c => c.MapFrom.IsNullOrWhiteSpace());
            RuleFor(c => c.GenerateImagePreviews)
                .Equal(false)
                .Unless(c => c.GeneratePreviews)
                .WithMessage("Generate Previews must also be enabled");
        }
    }

    public class StashSettings : NotificationSettingsBase<StashSettings>, IProviderConfig
    {
        private static readonly StashSettingsValidator Validator = new ();

        public StashSettings()
        {
            Port = 9999;
            StashBoxEndpoint = "https://stashdb.org/graphql";
            GenerateCovers = true;
            GeneratePreviews = true;
            GenerateSprites = true;
            GeneratePhashes = true;
        }

        [FieldDefinition(0, Label = "Host")]
        public string Host { get; set; }

        [FieldDefinition(1, Label = "Port")]
        public int Port { get; set; }

        [FieldDefinition(2, Label = "UseSsl", Type = FieldType.Checkbox, HelpText = "NotificationsSettingsUseSslHelpText")]
        [FieldToken(TokenField.HelpText, "UseSsl", "serviceName", "Stash")]
        public bool UseSsl { get; set; }

        [FieldDefinition(3, Label = "UrlBase", Type = FieldType.Textbox, Advanced = true, HelpText = "ConnectionSettingsUrlBaseHelpText")]
        [FieldToken(TokenField.HelpText, "UrlBase", "connectionName", "Stash")]
        [FieldToken(TokenField.HelpText, "UrlBase", "url", "http://[host]:[port]/[urlBase]/graphql")]
        public string UrlBase { get; set; }

        [FieldDefinition(4, Label = "ApiKey", HelpText = "NotificationsStashSettingsApiKeyHelpText", Privacy = PrivacyLevel.ApiKey)]
        public string ApiKey { get; set; }

        [FieldDefinition(5, Label = "NotificationsStashSettingsGenerateCovers", HelpText = "NotificationsStashSettingsGenerateCoversHelpText", Type = FieldType.Checkbox)]
        public bool GenerateCovers { get; set; }

        [FieldDefinition(6, Label = "NotificationsStashSettingsGeneratePreviews", HelpText = "NotificationsStashSettingsGeneratePreviewsHelpText", Type = FieldType.Checkbox)]
        public bool GeneratePreviews { get; set; }

        [FieldDefinition(7, Label = "NotificationsStashSettingsGenerateImagePreviews", HelpText = "NotificationsStashSettingsGenerateImagePreviewsHelpText", Type = FieldType.Checkbox)]
        public bool GenerateImagePreviews { get; set; }

        [FieldDefinition(8, Label = "NotificationsStashSettingsGenerateSprites", HelpText = "NotificationsStashSettingsGenerateSpritesHelpText", Type = FieldType.Checkbox)]
        public bool GenerateSprites { get; set; }

        [FieldDefinition(9, Label = "NotificationsStashSettingsGeneratePhashes", HelpText = "NotificationsStashSettingsGeneratePhashesHelpText", Type = FieldType.Checkbox)]
        public bool GeneratePhashes { get; set; }

        [FieldDefinition(10, Label = "NotificationsStashSettingsGenerateThumbnails", HelpText = "NotificationsStashSettingsGenerateThumbnailsHelpText", Type = FieldType.Checkbox, Advanced = true)]
        public bool GenerateThumbnails { get; set; }

        [FieldDefinition(11, Label = "NotificationsStashSettingsIdentify", HelpText = "NotificationsStashSettingsIdentifyHelpText", Type = FieldType.Checkbox)]
        public bool MetadataIdentify { get; set; }

        [FieldDefinition(12, Label = "NotificationsStashSettingsIdentifyStashBoxEndpoint", HelpText = "NotificationsStashSettingsIdentifyStashBoxEndpointHelpText", Type = FieldType.Textbox)]
        public string StashBoxEndpoint { get; set; }

        [FieldDefinition(13, Label = "NotificationsStashSettingsIdentifyBuiltinAutotag", HelpText = "NotificationsStashSettingsIdentifyBuiltinAutotagHelpText", Type = FieldType.Checkbox)]
        public bool BuiltinAutotag { get; set; }

        [FieldDefinition(14, Label = "NotificationsStashSettingsIdentifyIncludeMalePerformers", HelpText = "NotificationsStashSettingsIdentifyIncludeMalePerformersHelpText", Type = FieldType.Checkbox)]
        public bool IncludeMalePerformers { get; set; }

        [FieldDefinition(15, Label = "NotificationsStashSettingsIdentifySetCoverImage", HelpText = "NotificationsStashSettingsIdentifySetCoverImageHelpText", Type = FieldType.Checkbox)]
        public bool SetCoverImage { get; set; }

        [FieldDefinition(16, Label = "NotificationsStashSettingsIdentifySkipMultipleMatches", HelpText = "NotificationsStashSettingsIdentifySkipMultipleMatchesHelpText", Type = FieldType.Checkbox)]
        public bool SkipMultipleMatches { get; set; }

        [FieldDefinition(17, Label = "NotificationsStashSettingsIdentifySkipMultipleMatchTag", HelpText = "NotificationsStashSettingsIdentifySkipMultipleMatchTagHelpText", Type = FieldType.Number)]
        public int SkipMultipleMatchTag { get; set; }

        [FieldDefinition(18, Label = "NotificationsStashSettingsIdentifySetOrganized", HelpText = "NotificationsStashSettingsIdentifySetOrganizedHelpText", Type = FieldType.Checkbox)]
        public bool SetOrganized { get; set; }

        [FieldDefinition(19, Label = "NotificationsSettingsUpdateMapPathsFrom", HelpText = "NotificationsStashSettingsMapPathsFromHelpText", Type = FieldType.Textbox, Advanced = true)]
        public string MapFrom { get; set; }

        [FieldDefinition(20, Label = "NotificationsSettingsUpdateMapPathsTo", HelpText = "NotificationsStashSettingsMapPathsToHelpText", Type = FieldType.Textbox, Advanced = true)]
        public string MapTo { get; set; }

        [JsonIgnore]
        public string Address => $"{Host.ToUrlHost()}:{Port}{UrlBase}";

        public bool IsValid => !string.IsNullOrWhiteSpace(Host) && Port > 0;

        public override NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
