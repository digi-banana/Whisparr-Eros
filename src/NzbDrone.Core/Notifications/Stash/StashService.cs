using System;
using System.Collections.Generic;
using System.Net;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Movies;

namespace NzbDrone.Core.Notifications.Stash
{
    public interface IStashService
    {
        void Update(StashSettings settings, Movie movie);
        void Clean(StashSettings settings, Movie movie);
        string MapPath(StashSettings settings, string path);
        ValidationFailure Test(StashSettings settings);
    }

    public class StashService : IStashService
    {
        private readonly IStashProxy _proxy;
        private readonly ILocalizationService _localizationService;
        private readonly Logger _logger;

        public StashService(IStashProxy proxy, ILocalizationService localizationService, Logger logger)
        {
            _proxy = proxy;
            _localizationService = localizationService;
            _logger = logger;
        }

        public void Update(StashSettings settings, Movie movie)
        {
            var path = MapPath(settings, movie.Path);

            _logger.Debug("Asking Stash to scan {0}", path);
            _proxy.Scan(settings, path);

            if (settings.MetadataIdentify)
            {
                _logger.Debug("Asking Stash to identify {0}", path);
                _proxy.Identify(settings, path);
            }
        }

        public void Clean(StashSettings settings, Movie movie)
        {
            var path = MapPath(settings, movie.Path);

            _logger.Debug("Asking Stash to clean {0}", path);
            _proxy.Clean(settings, path);
        }

        public string MapPath(StashSettings settings, string path)
        {
            if (settings.MapFrom.IsNullOrWhiteSpace() || settings.MapTo.IsNullOrWhiteSpace())
            {
                return path;
            }

            var location = new OsPath(path);
            var mapFrom = new OsPath(settings.MapFrom);

            if (!mapFrom.Contains(location))
            {
                _logger.Debug("Path {0} is not under {1}, not mapping it for Stash", path, settings.MapFrom);
                return path;
            }

            var mapTo = new OsPath(settings.MapTo);
            var relative = (location - mapFrom).FullPath;

            if (relative.IsNullOrWhiteSpace() || relative == ".")
            {
                return mapTo.FullPath;
            }

            // Whisparr and Stash may run on different platforms, so join with Stash's separator
            var separator = mapTo.IsWindowsPath ? '\\' : '/';
            relative = relative.Replace('\\', separator).Replace('/', separator).TrimStart(separator);

            var mappedPath = mapTo.FullPath.TrimEnd('\\', '/') + separator + relative;

            _logger.Trace("Mapping path from {0} to {1} for Stash", path, mappedPath);

            return mappedPath;
        }

        public ValidationFailure Test(StashSettings settings)
        {
            try
            {
                _logger.Debug("Testing connection to Stash: {0}", settings.Address);

                var status = _proxy.GetStatus(settings);
                var systemStatus = status?.SystemStatus?.Status;

                _logger.Debug("Stash {0} status: {1}", status?.Version?.Version, systemStatus);

                if (!"OK".Equals(systemStatus, StringComparison.OrdinalIgnoreCase))
                {
                    return new ValidationFailure("Host", _localizationService.GetLocalizedString("NotificationsStashValidationNotReady", new Dictionary<string, object> { { "status", systemStatus ?? "unknown" } }));
                }
            }
            catch (HttpException ex) when (ex.Response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.Error(ex, "Unable to connect to Stash, API key rejected");
                return new ValidationFailure("ApiKey", _localizationService.GetLocalizedString("NotificationsValidationInvalidApiKey"));
            }
            catch (HttpException ex) when (ex.Response.StatusCode == HttpStatusCode.Forbidden)
            {
                _logger.Error(ex, "Unable to connect to Stash, access forbidden");
                return new ValidationFailure("ApiKey", _localizationService.GetLocalizedString("NotificationsStashValidationForbidden"));
            }
            catch (HttpException ex)
            {
                _logger.Error(ex, "Unable to connect to Stash");
                return new ValidationFailure("Host", _localizationService.GetLocalizedString("NotificationsValidationUnableToConnectToApi", new Dictionary<string, object> { { "service", "Stash" }, { "responseCode", (int)ex.Response.StatusCode }, { "exceptionMessage", ex.Message } }));
            }
            catch (StashException ex)
            {
                _logger.Error(ex, "Stash returned an error");
                return new ValidationFailure("Host", _localizationService.GetLocalizedString("NotificationsValidationUnableToSendTestMessageApiResponse", new Dictionary<string, object> { { "error", ex.Message } }));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unable to connect to Stash");
                return new ValidationFailure("Host", _localizationService.GetLocalizedString("NotificationsValidationUnableToConnect", new Dictionary<string, object> { { "exceptionMessage", ex.Message } }));
            }

            return null;
        }
    }
}
