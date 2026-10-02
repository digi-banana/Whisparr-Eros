using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NzbDrone.Common;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Movies.Credits;

namespace NzbDrone.Core.Movies
{
    /// <summary>
    /// Evidence for matching a release without a date ("Studio - Title - Performers") to one scene of its studio:
    /// the performers named in the release (by name, alias or credited name) and the issue / part / scene / volume numbers
    /// in the release and scene titles.
    /// </summary>
    public static class DatelessSceneEvidence
    {
        // Words that join performer names: "A & B", "A, B and C", "A x B", "A with B"
        private static readonly HashSet<string> JoinerWords = new (StringComparer.Ordinal) { "and", "x", "with", "feat", "ft", "featuring" };

        // Words that say nothing about which scene it is, ignored when comparing the rest of the release name with the scene title
        private static readonly HashSet<string> NeutralWords = new (StringComparer.Ordinal)
        {
            "a", "an", "the", "of", "in", "on", "at", "by", "to",
            "issue", "iss", "no", "nr", "vol", "volume", "part", "pt", "scene", "sc", "episode", "ep", "chapter", "ch",
            "sex", "solo"
        };

        // Numbers of a publication, which the studio and StashDB sometimes count one apart
        private static readonly HashSet<string> TolerantNumberKinds = new (StringComparer.Ordinal) { "issue", "vol", "episode" };

        private static readonly Regex TokenRegex = new (@"(?<separator>\s[-–—]+\s|[:|()\[\]{}""“”])|(?<joiner>[&+,])|(?<word>[\p{L}\p{N}]+)",
                                                        RegexOptions.Compiled,
                                                        RegexDefaults.Timeout);

        private static readonly Regex ApostropheRegex = new (@"['‘’`´]", RegexOptions.Compiled, RegexDefaults.Timeout);

        private static readonly Regex NumberRegex = new (@"\b(?<kind>issue|iss|volume|vol|part|pt|scene|sc|episode|ep|chapter|ch)\b\.?\s*(?:no\.?\s*|#\s*)?(?<number>\d{1,4})(?!\d)",
                                                         RegexOptions.Compiled | RegexOptions.IgnoreCase,
                                                         RegexDefaults.Timeout);

        private enum TokenKind
        {
            Word,
            Joiner,
            Separator
        }

        private readonly struct Token
        {
            public Token(TokenKind kind, string text)
            {
                Kind = kind;
                Text = text;
            }

            public TokenKind Kind { get; }
            public string Text { get; }

            public bool IsJoiner => Kind == TokenKind.Joiner || (Kind == TokenKind.Word && JoinerWords.Contains(Text));
        }

        /// <summary>
        /// True when the release names exactly the scene's performers: the scene has at least two performers, each of them is
        /// named in the release (by name, StashDB alias or credited name), the names form one list ("A &amp; B", "A, B and C")
        /// with no other name before, between or after them, and the rest of the release name doesn't contradict the scene title.
        /// </summary>
        /// <param name="releaseTokens">The release name after the studio, e.g. "Issue 389 - Gene Allen and Ashton Montana".</param>
        /// <param name="scene">The scene, with its credits loaded.</param>
        public static bool HasExactPerformers(string releaseTokens, Movie scene)
        {
            if (releaseTokens.IsNullOrWhiteSpace() || scene?.MovieMetadata?.Value?.Credits == null)
            {
                return false;
            }

            var performers = GetPerformerNames(scene.MovieMetadata.Value.Credits);

            // A single performer is weak evidence: performers make many scenes for a studio
            if (performers.Count < 2)
            {
                return false;
            }

            var tokens = Tokenize(releaseTokens);
            var spans = new List<(int Start, int End)>();

            foreach (var names in performers)
            {
                var span = FindName(tokens, names);

                if (span == null)
                {
                    return false;
                }

                spans.Add(span.Value);
            }

            spans = spans.OrderBy(s => s.Start).ToList();

            for (var i = 1; i < spans.Count; i++)
            {
                // Two performers found at the same place (shared name): can't tell them apart
                if (spans[i].Start < spans[i - 1].End)
                {
                    return false;
                }

                // Only joiners between the names, otherwise there is another name (or other words) in the list
                for (var t = spans[i - 1].End; t < spans[i].Start; t++)
                {
                    if (!tokens[t].IsJoiner)
                    {
                        return false;
                    }
                }
            }

            var first = spans[0].Start;
            var last = spans[spans.Count - 1].End;

            if (HasOtherName(tokens, first - 1, -1) || HasOtherName(tokens, last, 1))
            {
                return false;
            }

            var spanTokens = spans.SelectMany(s => Enumerable.Range(s.Start, s.End - s.Start)).ToHashSet();
            var performerWords = performers.SelectMany(n => n).SelectMany(n => n).ToHashSet(StringComparer.Ordinal);
            var studioWords = Tokenize(scene.MovieMetadata.Value.StudioTitle ?? string.Empty).Where(t => t.Kind == TokenKind.Word).Select(t => t.Text);

            var releaseWords = tokens.Where((t, i) => !spanTokens.Contains(i))
                                     .Where(IsTitleWord)
                                     .Select(t => t.Text)
                                     .Except(performerWords)
                                     .Except(studioWords)
                                     .ToHashSet(StringComparer.Ordinal);

            var sceneWords = Tokenize(scene.Title ?? string.Empty).Where(IsTitleWord)
                                                                   .Select(t => t.Text)
                                                                   .Except(performerWords)
                                                                   .Except(studioWords)
                                                                   .ToHashSet(StringComparer.Ordinal);

            // "Issue 389 - A & B" against "Issue 388, Sex Scene 2: A & B" is fine, "Hot Afternoon - A & B" against "Poolside" is not
            return releaseWords.Count == 0 || sceneWords.Count == 0 || releaseWords.IsSubsetOf(sceneWords);
        }

        /// <summary>
        /// True when the release names the scene title and one of the scene's performers (by name, StashDB alias or credited name)
        /// as whole words in different places of the release name. A one-word title or name only counts where it stands on its own:
        /// the scene "Alex" with performer "Alex" is not named by "Extra Credit - Alex Killborn &amp; Tyler Hill", where "Alex" is
        /// the start of another performer's name and the title and the performer would be the same word.
        /// </summary>
        /// <param name="releaseTokens">The release name after the studio, e.g. "Shower Sex - Joey Mills &amp; Landon Vega".</param>
        /// <param name="scene">The scene, with its credits loaded.</param>
        public static bool HasSeparateTitleAndPerformer(string releaseTokens, Movie scene)
        {
            if (releaseTokens.IsNullOrWhiteSpace() || scene == null || scene.Title.IsNullOrWhiteSpace() || scene.MovieMetadata?.Value?.Credits == null)
            {
                return false;
            }

            var tokens = Tokenize(releaseTokens);
            var titleWords = GetComparableWords(Tokenize(scene.Title));

            if (titleWords.Count == 0)
            {
                return false;
            }

            var titleSpans = FindTitle(tokens, titleWords);

            if (titleSpans.Count == 0)
            {
                return false;
            }

            foreach (var name in GetPerformerNames(scene.MovieMetadata.Value.Credits).SelectMany(n => n))
            {
                foreach (var span in FindAll(tokens, name))
                {
                    if (name.Count == 1 && !StandsAlone(tokens, span))
                    {
                        continue;
                    }

                    if (titleSpans.Any(t => t.End <= span.Start || span.End <= t.Start))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary> True when the release has the words of the scene title as whole words ("Alex" is not in "Alexander"). </summary>
        public static bool ContainsTitle(string releaseTokens, string sceneTitle)
        {
            var titleWords = GetComparableWords(Tokenize(sceneTitle));

            return titleWords.Count > 0 && FindAll(Tokenize(releaseTokens), titleWords).Any();
        }

        // The places the release names the title: its words as whole words, a one-word title on its own
        // ("Roommates" is named by "Roommates - Kyle Ross", not by "Hot Roommates" or "Roommates Kyle")
        private static List<(int Start, int End)> FindTitle(List<Token> tokens, List<string> titleWords)
        {
            return FindAll(tokens, titleWords).Where(s => titleWords.Count > 1 || StandsAlone(tokens, s)).ToList();
        }

        /// <summary>
        /// Compares the issue / part / scene / volume / episode / chapter numbers of the release and the scene title.
        /// Only numbers of a kind both carry are compared. For issue, volume and episode numbers a difference of one is neutral
        /// (studio sites and StashDB are often one issue apart) and a bigger difference is a conflict. Part, scene and chapter
        /// numbers tell the scenes of one issue apart, so any difference there is a conflict ("Part 1" is not "part 2").
        /// </summary>
        public static SceneNumberComparison CompareNumbers(string releaseTokens, string sceneTitle)
        {
            var result = new SceneNumberComparison();
            var releaseNumbers = GetNumbers(releaseTokens);
            var sceneNumbers = GetNumbers(sceneTitle);

            foreach (var (kind, number) in releaseNumbers)
            {
                if (!sceneNumbers.TryGetValue(kind, out var sceneNumber))
                {
                    continue;
                }

                var difference = Math.Abs(number - sceneNumber);

                if (difference == 0)
                {
                    result.ExactMatches++;
                }
                else if (difference > (TolerantNumberKinds.Contains(kind) ? 1 : 0))
                {
                    result.Conflict = true;
                }
            }

            return result;
        }

        private static Dictionary<string, int> GetNumbers(string title)
        {
            var numbers = new Dictionary<string, int>(StringComparer.Ordinal);

            if (title.IsNullOrWhiteSpace())
            {
                return numbers;
            }

            foreach (Match match in NumberRegex.Matches(title))
            {
                var kind = match.Groups["kind"].Value.ToLowerInvariant() switch
                {
                    "iss" => "issue",
                    "volume" => "vol",
                    "pt" => "part",
                    "sc" => "scene",
                    "ep" => "episode",
                    "ch" => "chapter",
                    var k => k
                };

                numbers.TryAdd(kind, int.Parse(match.Groups["number"].Value));
            }

            return numbers;
        }

        // Every performer's names (name, credited name, aliases), each as a list of words
        private static List<List<List<string>>> GetPerformerNames(IEnumerable<Credit> credits)
        {
            var performers = new List<List<List<string>>>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var credit in credits)
            {
                if (credit == null)
                {
                    continue;
                }

                var key = credit.Performer?.ForeignId.IsNotNullOrWhiteSpace() == true ? credit.Performer.ForeignId : credit.UniqueId ?? credit.Performer?.Name;

                if (key.IsNotNullOrWhiteSpace() && !seen.Add(key))
                {
                    continue;
                }

                var names = new[] { credit.Performer?.Name, credit.PersonName, credit.Character }
                    .Concat(credit.Performer?.Aliases ?? new List<string>())
                    .Where(n => n.IsNotNullOrWhiteSpace())
                    .Select(n => Tokenize(n).Where(t => t.Kind == TokenKind.Word).Select(t => t.Text).ToList())
                    .Where(n => n.Count > 0)
                    .DistinctBy(n => string.Join(" ", n))
                    .ToList();

                if (names.Any())
                {
                    performers.Add(names);
                }
            }

            return performers;
        }

        // The first place one of the names is in the release, preferring the longest name
        private static (int Start, int End)? FindName(List<Token> tokens, List<List<string>> names)
        {
            foreach (var name in names.OrderByDescending(n => n.Count))
            {
                for (var start = 0; start + name.Count <= tokens.Count; start++)
                {
                    var found = true;

                    for (var i = 0; i < name.Count; i++)
                    {
                        if (tokens[start + i].Kind != TokenKind.Word || tokens[start + i].Text != name[i])
                        {
                            found = false;
                            break;
                        }
                    }

                    if (found)
                    {
                        return (start, start + name.Count);
                    }
                }
            }

            return null;
        }

        // The words of a text as compared between a release and a title: "&" and "+" read as "and", other punctuation is skipped
        private static List<(int Index, string Text)> GetComparable(List<Token> tokens)
        {
            var words = new List<(int Index, string Text)>();

            for (var i = 0; i < tokens.Count; i++)
            {
                if (tokens[i].Kind == TokenKind.Word)
                {
                    words.Add((i, tokens[i].Text));
                }
                else if (tokens[i].Kind == TokenKind.Joiner && tokens[i].Text != ",")
                {
                    words.Add((i, "and"));
                }
            }

            return words;
        }

        private static List<string> GetComparableWords(List<Token> tokens)
        {
            return GetComparable(tokens).Select(w => w.Text).ToList();
        }

        // Every place the words are in the release, one after the other (punctuation between them aside), as token positions
        private static List<(int Start, int End)> FindAll(List<Token> tokens, List<string> words)
        {
            var spans = new List<(int Start, int End)>();
            var comparable = GetComparable(tokens);

            if (words.Count == 0)
            {
                return spans;
            }

            for (var start = 0; start + words.Count <= comparable.Count; start++)
            {
                var found = true;

                for (var i = 0; i < words.Count; i++)
                {
                    if (comparable[start + i].Text != words[i])
                    {
                        found = false;
                        break;
                    }
                }

                if (found)
                {
                    spans.Add((comparable[start].Index, comparable[start + words.Count - 1].Index + 1));
                }
            }

            return spans;
        }

        // Whether the words found are not part of a longer name or phrase: no other word right before or after them
        private static bool StandsAlone(List<Token> tokens, (int Start, int End) span)
        {
            return !IsPlainWord(tokens, span.Start - 1) && !IsPlainWord(tokens, span.End);
        }

        private static bool IsPlainWord(List<Token> tokens, int index)
        {
            return index >= 0 && index < tokens.Count && tokens[index].Kind == TokenKind.Word && !tokens[index].IsJoiner;
        }

        // Whether the list of names goes on before the first (direction -1) or after the last (direction 1) performer found:
        // a joiner next to the names followed by a word that could be a name, within the same part of the release name
        private static bool HasOtherName(List<Token> tokens, int index, int direction)
        {
            if (index < 0 || index >= tokens.Count || !tokens[index].IsJoiner)
            {
                return false;
            }

            for (var i = index; i >= 0 && i < tokens.Count && tokens[i].Kind != TokenKind.Separator; i += direction)
            {
                if (IsTitleWord(tokens[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsTitleWord(Token token)
        {
            return token.Kind == TokenKind.Word &&
                   !token.IsJoiner &&
                   !NeutralWords.Contains(token.Text) &&
                   !token.Text.All(char.IsDigit);
        }

        private static List<Token> Tokenize(string text)
        {
            var tokens = new List<Token>();

            if (text.IsNullOrWhiteSpace())
            {
                return tokens;
            }

            text = ApostropheRegex.Replace(text, string.Empty).RemoveDiacritics().ToLowerInvariant().Replace('.', ' ').Replace('_', ' ');

            foreach (Match match in TokenRegex.Matches(text))
            {
                if (match.Groups["separator"].Success)
                {
                    tokens.Add(new Token(TokenKind.Separator, match.Value));
                }
                else if (match.Groups["joiner"].Success)
                {
                    tokens.Add(new Token(TokenKind.Joiner, match.Value));
                }
                else
                {
                    tokens.Add(new Token(TokenKind.Word, match.Value));
                }
            }

            return tokens;
        }
    }

    /// <summary> How the numbers ("Issue 389", "Part 2") of a release compare to those of a scene title. </summary>
    public class SceneNumberComparison
    {
        /// <summary> A number of the same kind is more than one apart. </summary>
        public bool Conflict { get; set; }

        /// <summary> How many numbers of the same kind are equal. </summary>
        public int ExactMatches { get; set; }
    }
}
