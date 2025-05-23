using System;
using System.Threading.Tasks;
using PuppeteerSharp;

public class StatusCheckResult<T> where T : Enum
{
    public T StatusResult { get; private set; }
    public string Identity { get; private set; }
    public string Message { get; private set; }
    public StatusCheckResult(T statusResult, string identity, string message)
    {
        StatusResult = statusResult;
        Identity = identity;
        Message = message;
    }
}

public abstract class WebAutomatorBase<T> where T : Enum
{
    public abstract Task<StatusCheckResult<T>> CheckStatus(IPage page);

    protected async Task<string> GetElementTextContent(IPage page, string selector)
    {
        return await page.EvaluateFunctionAsync<string>(@"(selector) => {
            const el = document.querySelector(selector);
            return el ? el.textContent.trim() : null;
        }", selector);
    }

    protected async Task<string> GetInnerTextContent(IPage page, string selector)
    {
        return await page.EvaluateFunctionAsync<string>(@"(selector) => {
            const el = document.querySelector(selector);
            return el?.innerText?.trim();
        }", selector);
    }

    protected async Task<string> GetParentInnerTextContent(IPage page, string selector)
    {
        return await page.EvaluateFunctionAsync<string>(@"(selector) => {
            const el = document.querySelector(selector);
            return el?.parentElement?.innerText?.trim();
        }", selector);
    }

    protected async Task<string> GetElementAttributeValue(IPage page, string selector, string attribute)
    {
        return await page.EvaluateFunctionAsync<string>(@"(selector, attribute) => {
            const el = document.querySelector(selector);
            return el ? el.getAttribute(attribute) : null;
        }", selector, attribute);
    }

    protected async Task<T> GetElementFieldValue<T>(IPage page, string selector, string fieldName)
    {
        return await page.EvaluateFunctionAsync<T>(@"(selector, fieldName) => {
            const el = document.querySelector(selector);
            return el ? el[fieldName] : null;
        }", selector, fieldName);
    }

    protected async Task<T> SetElementFieldValue<T>(IPage page, string selector, string fieldName, T value)
    {
        return await page.EvaluateFunctionAsync<T>(@"(selector, fieldName, value) => {
            const el = document.querySelector(selector);
            if (el) {
                el[fieldName] = value;
            }
            return el ? el[fieldName] : null;
        }", selector, fieldName, value);
    }

    protected bool TryParseMinutesSecondsTimeSpan(string input, out TimeSpan result)
    {
        result = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        input = input.TrimStart('-');
        var parts = input.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[0], out int minutes) && int.TryParse(parts[1], out int seconds))
        {
            result = new TimeSpan(0, minutes, seconds);
            return true;
        }

        return false;
    }
}
