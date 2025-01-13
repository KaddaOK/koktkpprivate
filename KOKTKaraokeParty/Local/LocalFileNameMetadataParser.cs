using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

public class LocalFileNameMetadataParser
{
    public SongMetadata Parse(string fileName, string formatSpecification)
    {
        var metadata = new SongMetadata();
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

        // Match the file name against the regex pattern
        var regex = new Regex(GetRegexEquivalent(formatSpecification), RegexOptions.IgnoreCase);
        var match = regex.Match(fileName);

        if (match.Success)
        {
            metadata.Identifier = MatchOrNull(match.Groups, "identifier");
            metadata.ArtistName = MatchOrNull(match.Groups, "artist");
            metadata.SongTitle = MatchOrNull(match.Groups, "title");
            metadata.CreatorName = MatchOrNull(match.Groups, "creator");
        }

        return metadata;
    }

    string outputRegexToMatchEitherPathSeparator = @"[\\/]";
    string outputRegexForExtensionAndFileEnd = @"\..+$";
    string outputRegexForDiscardedFolder = @"(?:[^\\/]+[\\/])";
    string matcherForAlreadyReplacedPathSeparators => Regex.Escape(outputRegexToMatchEitherPathSeparator);
    private string matcherForPathWildcard(object numberOfPaths) => @$"{matcherForAlreadyReplacedPathSeparators}(?:\\\*){{{numberOfPaths}}}{matcherForAlreadyReplacedPathSeparators}";
    private string namedCapture(string name) => @$"(?<{name}>[^\\/]+?)";

    private string AllowEitherPathSeparatorInAlreadyEscapedInput(string alreadyEscapedInput)
    {
        return Regex.Replace(alreadyEscapedInput, @"(?:\\\\|/)", outputRegexToMatchEitherPathSeparator);
    }

    private string ReplaceDirectoryMatchersInAlreadyReplacedInput(string alreadyReplacedInput)
    {

        return
        Regex.Replace(
            // double asterisks get an asterisk
            Regex.Replace(alreadyReplacedInput, matcherForPathWildcard("2,"), @$"{outputRegexToMatchEitherPathSeparator}{outputRegexForDiscardedFolder}*")
        // single asterisks do not
        , matcherForPathWildcard(1), @$"{outputRegexToMatchEitherPathSeparator}{outputRegexForDiscardedFolder}");

    }

    private string ReplaceAnyRemainingUnescapedAsterisks(string alreadyReplacedInput)
    {
        return alreadyReplacedInput.Replace(@"\*", @"[^\\/]*");
    }

    public string GetRegexEquivalent(string formatSpecification)
    {
        if (formatSpecification.Contains("}*") || formatSpecification.Contains("*{"))
        {
            throw new ArgumentException("An asterisk ('*') cannot go right next to a {token}.");
        }
        if (Regex.IsMatch(formatSpecification, @$"{outputRegexToMatchEitherPathSeparator}\*+{outputRegexToMatchEitherPathSeparator}\*+{outputRegexToMatchEitherPathSeparator}"))
        {
            throw new ArgumentException("It is an error to have two directory wildcards ('/*/' or '/**/') in a row.");
        }

        // there's no value in including any leading slashes or asterisks because we're not using the ^ start
        formatSpecification = formatSpecification.TrimStart('*', '/', '\\');

        // escape the user's input for regex literally
        var escapedFormat = Regex.Escape(formatSpecification);

        // they should be able to use either \ or / and have it work correctly on any platform
        escapedFormat = AllowEitherPathSeparatorInAlreadyEscapedInput(escapedFormat);

        // here we replace the /*/ or /**/ wildcards with folder matchers
        escapedFormat = ReplaceDirectoryMatchersInAlreadyReplacedInput(escapedFormat);

        // Replace placeholders with regex groups
        escapedFormat = escapedFormat.Replace(@"\{identifier}", namedCapture("identifier"), StringComparison.InvariantCultureIgnoreCase)
                                    .Replace(@"\{artist}", namedCapture("artist"), StringComparison.InvariantCultureIgnoreCase)
                                    .Replace(@"\{title}", namedCapture("title"), StringComparison.InvariantCultureIgnoreCase)
                                    .Replace(@"\{creator}", namedCapture("creator"), StringComparison.InvariantCultureIgnoreCase);

        // Replace any remaining asterisks
        escapedFormat = ReplaceAnyRemainingUnescapedAsterisks(escapedFormat);

        // expect the extension and end of the string
        escapedFormat += outputRegexForExtensionAndFileEnd;

        // TODO: probably don't want any leading slashes?

        return escapedFormat;
    }

    public string MatchOrNull(GroupCollection groups, string name)
    {
        if (!groups.ContainsKey(name)
            || !groups[name].Success)
        {
            return null;
        }

        return groups[name].Value;
    }
}

public class SongMetadata
{
    public string ArtistName { get; set; }
    public string SongTitle { get; set; }
    public string CreatorName { get; set; }
    public string Identifier { get; set; }
}