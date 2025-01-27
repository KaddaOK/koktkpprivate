using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

public interface ILocalFileNameMetadataParser
{
    SongMetadata Parse(string fileName, string formatSpecification);
    string GetRegexEquivalent(string formatSpecification);
    (bool isValid, string validationError) ValidateFormatSpecification(string formatSpecification);
}

public class LocalFileNameMetadataParser : ILocalFileNameMetadataParser
{
    public SongMetadata Parse(string fileName, string formatSpecification)
    {
        var metadata = new SongMetadata();
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

        // if the formatSpecification starts with a *, / or \, we should start the filename with one as well, so that I don't die of frustration
        if (formatSpecification.StartsWith("*") || formatSpecification.StartsWith("/") || formatSpecification.StartsWith("\\"))
        {
            fileName = "/" + fileName;
        }

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

    string eitherSlash = @"[\\/]";
    string noSlashesBehind = @$"(?<![\\/])";
    string noSlashesAhead = @"(?![\\/])";
    string extensionAndFileEnd = @"\..+$";
    string outputRegexForDiscardedFolder = @"(?:[^\\/]+[\\/])";
    string alreadyReplacedEitherSlash => Regex.Escape(eitherSlash);
    private string matcherForPathWildcard(object numberOfPaths) => @$"{alreadyReplacedEitherSlash}(?:\\\*){{{numberOfPaths}}}{alreadyReplacedEitherSlash}";
    private string namedCapture(string name) => @$"(?<{name}>[^\\/]+?)";

    private string AllowEitherPathSeparatorInAlreadyEscapedInput(string alreadyEscapedInput)
    {
        return Regex.Replace(alreadyEscapedInput, @"(?:\\\\|/)", eitherSlash);
    }

    private string ReplaceDirectoryMatchersInAlreadyReplacedInput(string alreadyReplacedInput)
    {

        return
        Regex.Replace(
            // double asterisks get an asterisk
            Regex.Replace(alreadyReplacedInput, matcherForPathWildcard("2,"), @$"{eitherSlash}{outputRegexForDiscardedFolder}*")
        // single asterisks do not
        , matcherForPathWildcard(1), @$"{eitherSlash}{outputRegexForDiscardedFolder}");

    }

    private string ReplaceAnyRemainingUnescapedAsterisks(string alreadyReplacedInput)
    {
        return alreadyReplacedInput.Replace(@"\*", @"[^\\/]*");
    }

    public (bool isValid, string validationError) ValidateFormatSpecification(string formatSpecification)
    {
        if (formatSpecification.Contains("}*") || formatSpecification.Contains("*{"))
        {
            return (false, "An asterisk ('*') cannot go right next to a {token}.");
        }
        var badAsterisksMatch = Regex.Match(formatSpecification, @$"{eitherSlash}\*{{3,}}{eitherSlash}");
        if (badAsterisksMatch.Success)
        {
            return (false, $"Bad syntax at {badAsterisksMatch.Index}: '{badAsterisksMatch.Value}' (maximum 2 asterisks in a row is allowed inside slashes)");
        }
        badAsterisksMatch = Regex.Match(formatSpecification, $@"{noSlashesBehind}\*{{2,}}|\*{{2,}}{noSlashesAhead}");
        if (badAsterisksMatch.Success)
        {
            return (false, $"Bad syntax at {badAsterisksMatch.Index}: '{badAsterisksMatch.Value}' (only one asterisk in a row is allowed when not wrapped in slashes)");
        }
        if (Regex.IsMatch(formatSpecification, @$"{eitherSlash}\*\*{eitherSlash}\*{eitherSlash}") ||
            Regex.IsMatch(formatSpecification, @$"{eitherSlash}\*{eitherSlash}\*\*{eitherSlash}"))
        {
            return (false, "Single-asterisk paths /*/ cannot be adjacent to double-asterisk paths /**/ in the format specifier.");
        }
        if (Regex.Matches(formatSpecification, @$"\*\*").Count > 1)
        {
            return (false, "You cannot have more than one double-asterisk path /**/ in the format specifier.");
        }

        return (true, null);
    }

    public string GetRegexEquivalent(string formatSpecification)
    {
        var validation = ValidateFormatSpecification(formatSpecification);
        if (!validation.isValid)
        {
            throw new ArgumentException(validation.validationError);
        }

        // there's no value in including any leading slashes or asterisks because we're not using the ^ start
        //formatSpecification = formatSpecification.TrimStart('*', '/', '\\');
        // ok no that got a bit messed up, so I guess instead if it starts with an asterisk let's add a slash and we'll also do so when parsing?  idfk
        if (formatSpecification.StartsWith("*")) {
            formatSpecification = "/" + formatSpecification;
        }

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
        escapedFormat += extensionAndFileEnd;

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