using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.MediaFiles.MovieImport
{
    public interface IDetectSample
    {
        DetectSampleResult IsSample(MovieMetadata movie, string path);
        DetectSampleResult IsSample(LocalMovie localMovie);
    }

    public class DetectSample : IDetectSample
    {
        // A scene file whose runtime cannot be read is only treated as not being a sample at or above this size.
        // Release samples are a few seconds to a minute of video and stay well below it.
        public static readonly long SceneUnknownRuntimeMinimumSize = 100.Megabytes();

        private static readonly Regex SampleNameRegex = new Regex(@"(?<![a-z0-9])samples?(?![a-z0-9])", RegexOptions.Compiled | RegexOptions.IgnoreCase, RegexDefaults.Timeout);

        private readonly IVideoFileInfoReader _videoFileInfoReader;
        private readonly Logger _logger;

        public DetectSample(IVideoFileInfoReader videoFileInfoReader, Logger logger)
        {
            _videoFileInfoReader = videoFileInfoReader;
            _logger = logger;
        }

        public DetectSampleResult IsSample(MovieMetadata movie, string path)
        {
            return IsSample(movie, path, () => _videoFileInfoReader.GetRunTime(path));
        }

        public DetectSampleResult IsSample(LocalMovie localMovie)
        {
            MovieMetadata movie = localMovie.Movie.MovieMetadata;

            if (movie?.ItemType != ItemType.Scene)
            {
                return IsSample(movie, localMovie.Path);
            }

            // Re-use the media info read while augmenting the file rather than running ffprobe again;
            // a second read is slow over network/FUSE mounts and can fail where the first one succeeded.
            var result = IsSample(movie,
                                  localMovie.Path,
                                  () => localMovie.MediaInfo != null ? localMovie.MediaInfo.RunTime : _videoFileInfoReader.GetRunTime(localMovie.Path));

            if (result != DetectSampleResult.Indeterminate)
            {
                return result;
            }

            return IsSceneSampleWithoutRuntime(localMovie);
        }

        private DetectSampleResult IsSceneSampleWithoutRuntime(LocalMovie localMovie)
        {
            var fileName = Path.GetFileName(localMovie.Path) ?? string.Empty;
            var folderName = Path.GetFileName(Path.GetDirectoryName(localMovie.Path)) ?? string.Empty;

            if (SampleNameRegex.IsMatch(fileName) || SampleNameRegex.IsMatch(folderName))
            {
                _logger.Debug("[{0}] runtime is unknown and its name suggests a sample", localMovie.Path);
                return DetectSampleResult.Indeterminate;
            }

            if (localMovie.Size < SceneUnknownRuntimeMinimumSize)
            {
                _logger.Debug("[{0}] runtime is unknown and its size of {1} is below {2}", localMovie.Path, localMovie.Size.SizeSuffix(), SceneUnknownRuntimeMinimumSize.SizeSuffix());
                return DetectSampleResult.Indeterminate;
            }

            _logger.Warn("Unable to read the runtime of [{0}], treating it as not a sample based on its size of {1}", localMovie.Path, localMovie.Size.SizeSuffix());
            return DetectSampleResult.NotSample;
        }

        private DetectSampleResult IsSample(MovieMetadata movie, string path, Func<TimeSpan?> getRunTime)
        {
            var extension = Path.GetExtension(path);

            if (extension != null)
            {
                if (extension.Equals(".flv", StringComparison.InvariantCultureIgnoreCase))
                {
                    _logger.Debug("Skipping sample check for .flv file");
                    return DetectSampleResult.NotSample;
                }

                if (extension.Equals(".strm", StringComparison.InvariantCultureIgnoreCase))
                {
                    _logger.Debug("Skipping sample check for .strm file");
                    return DetectSampleResult.NotSample;
                }

                if (new string[] { ".iso", ".img", ".m2ts" }.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    _logger.Debug($"Skipping sample check for DVD/BR image file '{path}'");
                    return DetectSampleResult.NotSample;
                }
            }

            var runTime = getRunTime();

            if (!runTime.HasValue)
            {
                _logger.Error("Failed to get runtime from the file, make sure ffprobe is available");
                return DetectSampleResult.Indeterminate;
            }

            var minimumRuntime = GetMinimumAllowedRuntime(movie);

            if (runTime.Value.TotalMinutes.Equals(0))
            {
                _logger.Error("[{0}] has a runtime of 0, is it a valid video file?", path);
                return DetectSampleResult.Sample;
            }

            if (runTime.Value.TotalSeconds < minimumRuntime)
            {
                _logger.Debug("[{0}] appears to be a sample. Runtime: {1} seconds. Expected at least: {2} seconds", path, runTime.Value.TotalSeconds, minimumRuntime);
                return DetectSampleResult.Sample;
            }

            _logger.Debug("[{0}] does not appear to be a sample. Runtime {1} seconds is more than minimum of {2} seconds", path, runTime, minimumRuntime);
            return DetectSampleResult.NotSample;
        }

        private int GetMinimumAllowedRuntime(MovieMetadata movie)
        {
            // Anime short - 15 seconds
            if (movie.Runtime <= 3)
            {
                return 15;
            }

            // Webisodes - 90 seconds
            if (movie.Runtime <= 10)
            {
                return 90;
            }

            // 30 minute episodes - 5 minutes
            if (movie.Runtime <= 30)
            {
                return 300;
            }

            // 60 minute episodes - 10 minutes
            return 600;
        }
    }
}
