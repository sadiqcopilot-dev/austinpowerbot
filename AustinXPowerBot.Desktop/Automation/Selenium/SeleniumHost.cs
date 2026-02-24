using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;

namespace AustinXPowerBot.Desktop.Automation.Selenium;

public sealed class SeleniumHost : IAsyncDisposable
{
    private const string DefaultBrokerUrl = "https://pocketoption.com";
    private IWebDriver? _driver;
    private BrowserType? _activeBrowserType;
    private Process? _chromeProcess;

    public bool IsDriverReady { get; private set; }
    public bool IsPageLoaded { get; private set; }
    public bool IsLoginDetected { get; private set; }

    public event Action<string>? Log;

    public enum AccountMode
    {
        Unknown,
        Real,
        Demo
    }

    public sealed record TradeStatsSnapshot(int OpenTrades, int ClosedTrades, int Wins, int Losses, decimal WinRate);

    public Task StartBrowser(ProfileMode profileMode, BrowserType browserType, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_driver is not null && _activeBrowserType == browserType)
        {
            EnsureBrokerPage();
            IsDriverReady = true;
            IsPageLoaded = true;
            Log?.Invoke($"Browser reused: {browserType} ({profileMode})");
            Log?.Invoke($"Navigated to: {DefaultBrokerUrl}");
            return Task.CompletedTask;
        }

        CloseBrowserInternal();

        var profilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AustinXPowerBot",
            "BrowserProfile");

        Directory.CreateDirectory(profilePath);

        _driver = browserType switch
        {
            BrowserType.Chrome => BuildChrome(profileMode, profilePath),
            BrowserType.Edge => BuildEdge(profileMode, profilePath),
            _ => throw new ArgumentOutOfRangeException(nameof(browserType), browserType, null)
        };

        _activeBrowserType = browserType;
        _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(500);
        EnsureBrokerPage();

        IsDriverReady = true;
        IsPageLoaded = true;
        IsLoginDetected = false;

        Log?.Invoke($"Browser started: {browserType} ({profileMode})");
        Log?.Invoke($"Navigated to: {DefaultBrokerUrl}");
        return Task.CompletedTask;
    }

    public Task AttachSession(string sessionDescriptor, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Log?.Invoke($"AttachSession requested: {sessionDescriptor}");
        IsDriverReady = _driver is not null;
        return Task.CompletedTask;
    }

    public Task CloseBrowser(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CloseBrowserInternal();
        Log?.Invoke("Browser closed.");
        return Task.CompletedTask;
    }

    public Task<decimal?> ReadBalanceAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_driver is null)
        {
            return Task.FromResult<decimal?>(null);
        }

        try
        {
            var realBalance = ReadBalanceByLabel("qt real");
            return Task.FromResult(realBalance);
        }
        catch (Exception ex)
        {
            Log?.Invoke($"ReadBalance failed: {ex.Message}");
        }

        return Task.FromResult<decimal?>(null);
    }

    public async Task<bool> SetStakeAmountAsync(decimal amount, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_driver is null)
        {
            return false;
        }

        try
        {
            EnsureBrokerPage();
            var js = (IJavaScriptExecutor)_driver;
            var invariantAmount = amount.ToString("0.##", CultureInfo.InvariantCulture);

            var script = """
                const value = arguments[0];
                const isVisible = (el) => !!(el && (el.offsetWidth || el.offsetHeight || el.getClientRects().length));
                const toNumber = (raw) => {
                    const txt = (raw || '').toString().replace(/[^0-9.,-]/g, '').replace(/,/g, '').trim();
                    const num = Number(txt);
                    return Number.isFinite(num) ? num : NaN;
                };

                const desired = toNumber(value);
                if (!Number.isFinite(desired)) return false;

                const preferred = Array.from(document.querySelectorAll("input[type='text'], input[type='tel'], input[type='number'], input[autocomplete='off'], input[inputmode='decimal'], input[inputmode='numeric'], [role='spinbutton'], [role='textbox']"))
                    .filter(isVisible)
                    .filter((el) => !el.disabled && !el.readOnly);

                let target = preferred.find((el) => {
                    const current = (el.value || '').trim();
                    return current === '' || /^\d+(\.\d+)?$/.test(current);
                }) || preferred[0] || null;

                const candidates = Array.from(document.querySelectorAll('input, textarea'))
                    .filter(isVisible)
                    .filter((el) => {
                        const hay = ((el.name || '') + ' ' + (el.id || '') + ' ' + (el.className || '') + ' ' + (el.placeholder || '')).toLowerCase();
                        const type = (el.type || '').toLowerCase();
                        return hay.includes('amount')
                            || hay.includes('stake')
                            || hay.includes('invest')
                            || hay.includes('trade')
                            || hay.includes('investment')
                            || hay.includes('sum')
                            || type === 'number'
                            || type === 'tel';
                    });

                target = target || candidates[0] || null;

                if (!target) {
                    const byDataOrClass = Array.from(document.querySelectorAll('[data-qa], [data-test], [class], [id]'))
                        .filter(isVisible)
                        .filter((el) => {
                            const hay = ((el.getAttribute('data-qa') || '') + ' ' + (el.getAttribute('data-test') || '') + ' ' + (el.id || '') + ' ' + (el.className || '')).toLowerCase();
                            if (!(hay.includes('amount') || hay.includes('stake') || hay.includes('invest') || hay.includes('investment') || hay.includes('sum'))) {
                                return false;
                            }

                            const inputInside = el.querySelector('input, textarea, [contenteditable="true"], [role="spinbutton"], [role="textbox"]');
                            if (inputInside) {
                                target = inputInside;
                                return true;
                            }

                            const txt = (el.textContent || '').trim();
                            return /\d/.test(txt);
                        });

                    if (!target) {
                        target = byDataOrClass[0] || null;
                    }
                }

                if (!target) {
                    const editable = Array.from(document.querySelectorAll("[contenteditable='true']"))
                        .filter(isVisible)
                        .find((el) => /\d/.test((el.textContent || '').trim()));
                    target = editable || null;
                }

                if (!target) return false;

                const applyInputEvents = (el) => {
                    el.dispatchEvent(new Event('input', { bubbles: true }));
                    el.dispatchEvent(new Event('change', { bubbles: true }));
                    el.dispatchEvent(new KeyboardEvent('keyup', { bubbles: true, key: 'Enter' }));
                    el.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, key: 'Enter' }));
                    el.dispatchEvent(new KeyboardEvent('keypress', { bubbles: true, key: 'Enter' }));
                };

                const setInputValue = (input, newValue) => {
                    const prototype = Object.getPrototypeOf(input);
                    const descriptor = Object.getOwnPropertyDescriptor(prototype, 'value') || Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value');
                    if (descriptor && typeof descriptor.set === 'function') {
                        descriptor.set.call(input, newValue);
                    } else {
                        input.value = newValue;
                    }
                };

                target.focus();

                if (target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement) {
                    setInputValue(target, '');
                    applyInputEvents(target);
                    setInputValue(target, value);
                    applyInputEvents(target);
                } else {
                    target.textContent = value;
                    target.innerText = value;
                    applyInputEvents(target);
                }

                target.blur();

                const readBackRaw = (target.value || target.textContent || '').toString();
                const readBackNum = toNumber(readBackRaw);
                if (!Number.isFinite(readBackNum)) {
                    return true;
                }

                return Math.abs(readBackNum - desired) < 0.01;
                """;

            var success = false;
            for (var attempt = 1; attempt <= 4; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _driver.SwitchTo().DefaultContent();
                var result = js.ExecuteScript(script, invariantAmount);
                success = result is bool b && b;

                if (!success)
                {
                    var frames = _driver.FindElements(By.CssSelector("iframe, frame"));
                    foreach (var frame in frames)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            _driver.SwitchTo().DefaultContent();
                            _driver.SwitchTo().Frame(frame);
                            result = js.ExecuteScript(script, invariantAmount);
                            success = result is bool frameResult && frameResult;
                            if (success)
                            {
                                break;
                            }
                        }
                        catch
                        {
                        }
                    }
                }

                _driver.SwitchTo().DefaultContent();
                if (success)
                {
                    break;
                }

                await Task.Delay(300, cancellationToken);
            }

            Log?.Invoke(success
                ? $"Stake amount synced to broker: {invariantAmount}"
                : "Stake amount sync failed: amount input could not be written or verified on page.");
            return success;
        }
        catch (Exception ex)
        {
            Log?.Invoke($"SetStakeAmount failed: {ex.Message}");
            return false;
        }
    }

    public Task<decimal?> ReadDemoBalanceAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_driver == null)
        {
            return Task.FromResult<decimal?>(null);
        }

        try
        {
            var demoBalance = ReadBalanceByLabel("qt demo");
            return Task.FromResult(demoBalance);
        }
        catch
        {
            return Task.FromResult<decimal?>(null);
        }
    }

    private decimal? ReadBalanceByLabel(string labelContains)
    {
        if (_driver == null)
        {
            return null;
        }

        var js = (IJavaScriptExecutor)_driver;

        var balanceText = js.ExecuteScript(@"
            const token = (arguments[0] || '').toString().trim().toLowerCase();
            const labels = Array.from(document.querySelectorAll('div.balance-info-block__label'));
            const matchedLabel = labels.find(el => (el.textContent || '').trim().toLowerCase().includes(token));
            if (!matchedLabel) return null;

            const container = matchedLabel.closest('div.balance-info-block') || matchedLabel.parentElement;
            if (!container) return null;

            const candidates = [
                ...container.querySelectorAll('div.balance-info-block__sum-value'),
                ...container.querySelectorAll('div.balance-info-block__sum'),
                ...container.querySelectorAll('span'),
                ...container.querySelectorAll('div')
            ];

            for (const candidate of candidates) {
                const text = (candidate.textContent || '').trim();
                if (!text) continue;
                if (/\d/.test(text)) return text;
            }

            const fallback = (container.textContent || '').trim();
            return fallback || null;
        ", labelContains)?.ToString();

        if (string.IsNullOrWhiteSpace(balanceText))
        {
            return null;
        }

        var match = Regex.Match(balanceText, @"([0-9]{1,3}(?:[,\s][0-9]{3})*(?:\.[0-9]{1,2})?|[0-9]+(?:\.[0-9]{1,2})?)");
        if (match.Success && TryParseBalance(match.Groups[1].Value, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    public Task<bool> SetExpirationAsync(string expirationText, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_driver is null)
        {
            return Task.FromResult(false);
        }

        try
        {
            EnsureBrokerPage();
            var js = (IJavaScriptExecutor)_driver;

            var script = """
                const value = arguments[0];
                const isVisible = (el) => !!(el && (el.offsetWidth || el.offsetHeight || el.getClientRects().length));
                const timeRegex = /^\d{2}:\d{2}:\d{2}$/;

                if (!timeRegex.test(value)) {
                    return false;
                }

                const toSeconds = (raw) => {
                    const text = (raw || '').toString().trim();
                    if (!text) return NaN;

                    const hhmmss = text.match(/^(\d{2}):(\d{2}):(\d{2})$/);
                    if (hhmmss) {
                        const hh = Number(hhmmss[1]);
                        const mm = Number(hhmmss[2]);
                        const ss = Number(hhmmss[3]);
                        return (hh * 3600) + (mm * 60) + ss;
                    }

                    const mmss = text.match(/^(\d{1,2}):(\d{2})$/);
                    if (mmss) {
                        const mm = Number(mmss[1]);
                        const ss = Number(mmss[2]);
                        return (mm * 60) + ss;
                    }

                    const secMatch = text.match(/(\d+)\s*(s|sec|secs|second|seconds)$/i);
                    if (secMatch) {
                        return Number(secMatch[1]);
                    }

                    return NaN;
                };

                const expectedSeconds = toSeconds(value);

                const toPanelToken = (seconds) => {
                    if (!Number.isFinite(seconds) || seconds <= 0) return null;
                    if (seconds < 60) return `S${Math.round(seconds)}`;
                    if (seconds % 3600 === 0) return `H${Math.round(seconds / 3600)}`;
                    if (seconds % 60 === 0) return `M${Math.round(seconds / 60)}`;
                    return null;
                };

                const panelMatchesSeconds = (text, seconds) => {
                    if (!Number.isFinite(seconds)) return false;
                    const t = (text || '').trim().toUpperCase();
                    if (!t) return false;

                    if (seconds < 60) {
                        return t === `S${seconds}`
                            || t === `${seconds}S`
                            || t === `${seconds} SEC`
                            || t === `${seconds} SECS`
                            || t === `${seconds} SECOND`
                            || t === `${seconds} SECONDS`
                            || t === `00:${seconds.toString().padStart(2, '0')}`;
                    }

                    if (seconds % 3600 === 0) {
                        const h = Math.round(seconds / 3600);
                        return t === `H${h}`
                            || t === `${h}H`
                            || t === `${h} HR`
                            || t === `${h} HOUR`
                            || t === `${h} HOURS`;
                    }

                    if (seconds % 60 === 0) {
                        const m = Math.round(seconds / 60);
                        return t === `M${m}`
                            || t === `${m}M`
                            || t === `${m} MIN`
                            || t === `${m} MINS`
                            || t === `${m} MINUTE`
                            || t === `${m} MINUTES`
                            || t === `${m}:00`;
                    }

                    return false;
                };

                const fireEvents = (el) => {
                    el.dispatchEvent(new Event('input', { bubbles: true }));
                    el.dispatchEvent(new Event('change', { bubbles: true }));
                    el.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, key: 'Enter' }));
                    el.dispatchEvent(new KeyboardEvent('keyup', { bubbles: true, key: 'Enter' }));
                    el.dispatchEvent(new FocusEvent('blur', { bubbles: true }));
                };

                const setValue = (el, newValue) => {
                    try {
                        const proto = Object.getPrototypeOf(el);
                        const descriptor = Object.getOwnPropertyDescriptor(proto, 'value')
                            || Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')
                            || Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, 'value');
                        if (descriptor && typeof descriptor.set === 'function') {
                            descriptor.set.call(el, newValue);
                            return true;
                        }
                    } catch {}

                    try {
                        el.value = newValue;
                        return true;
                    } catch {
                        return false;
                    }
                };

                const findDisplay = () => {
                    const displayCandidates = Array.from(document.querySelectorAll('div.value__val, .value__val, [class*="expiration"], [class*="expiry"], [data-qa*="expiration"], [data-qa*="expiry"]'))
                        .filter(isVisible);

                    for (const el of displayCandidates) {
                        const txt = (el.textContent || '').trim();
                        if (timeRegex.test(txt)) return el;
                    }

                    return displayCandidates.find((el) => timeRegex.test((el.textContent || '').trim())) || null;
                };

                const verify = () => {
                    const display = findDisplay();
                    if (!display) return false;
                    const txt = (display.textContent || '').trim();
                    if (txt === value) return true;

                    const actualSeconds = toSeconds(txt);
                    if (Number.isFinite(expectedSeconds) && Number.isFinite(actualSeconds)) {
                        return Math.abs(actualSeconds - expectedSeconds) <= 2;
                    }

                    return false;
                };

                const editableInputs = Array.from(document.querySelectorAll("input, textarea, [contenteditable='true']"))
                    .filter(isVisible)
                    .filter((el) => {
                        const hay = ((el.name || '') + ' ' + (el.id || '') + ' ' + (el.className || '') + ' ' + (el.placeholder || '') + ' ' + (el.getAttribute?.('aria-label') || '')).toLowerCase();
                        return hay.includes('expir') || hay.includes('time') || hay.includes('duration') || timeRegex.test((el.value || el.textContent || '').trim());
                    });

                for (const target of editableInputs) {
                    try { target.focus(); } catch {}

                    if (target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement) {
                        if (!setValue(target, value)) continue;
                        fireEvents(target);
                    } else {
                        target.textContent = value;
                        target.innerText = value;
                        fireEvents(target);
                    }

                    if (verify()) return true;
                }

                const options = Array.from(document.querySelectorAll('*')).filter(isVisible)
                    .filter((el) => {
                        const txt = (el.textContent || '').trim();
                        if (!txt) return false;
                        if (txt === value || txt.includes(value)) return true;

                        const optionSeconds = toSeconds(txt);
                        return Number.isFinite(expectedSeconds) && Number.isFinite(optionSeconds) && optionSeconds === expectedSeconds;
                    })
                    .slice(0, 30);

                const displayForOpen = findDisplay();
                if (displayForOpen) {
                    try {
                        displayForOpen.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true, view: window }));
                        displayForOpen.dispatchEvent(new MouseEvent('mouseup', { bubbles: true, cancelable: true, view: window }));
                        displayForOpen.click();
                    } catch {}
                }

                const token = toPanelToken(expectedSeconds);
                if (token) {
                    const panelItems = Array.from(document.querySelectorAll('.trading-panel-modal__dops .dops__timeframes-item, .dops__timeframes .dops__timeframes-item'))
                        .filter(isVisible);

                    const match = panelItems.find((el) => {
                        const txt = (el.textContent || '').trim();
                        return txt.toUpperCase() === token.toUpperCase() || panelMatchesSeconds(txt, expectedSeconds);
                    }) || null;
                    if (match) {
                        const clickable = match.closest('.dops__timeframes-item, button, [role="button"], div') || match;
                        try { clickable.scrollIntoView({ block: 'center', inline: 'center' }); } catch {}
                        clickable.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true, view: window }));
                        clickable.dispatchEvent(new MouseEvent('mouseup', { bubbles: true, cancelable: true, view: window }));
                        clickable.click();

                        const becameActive = (match.className || '').toString().toLowerCase().includes('dops__timeframes-item--active');
                        const activeItem = panelItems.find((el) => (el.className || '').toString().toLowerCase().includes('dops__timeframes-item--active')) || null;
                        const activeText = (activeItem?.textContent || '').trim();
                        const activeMatches = panelMatchesSeconds(activeText, expectedSeconds)
                            || activeText.toUpperCase() === token.toUpperCase();

                        if (becameActive || activeMatches || verify()) return true;

                        return true;
                    }
                }

                for (const option of options) {
                    const clickable = option.closest('button, [role="button"], li, div') || option;
                    try { clickable.scrollIntoView({ block: 'center', inline: 'center' }); } catch {}
                    clickable.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true, view: window }));
                    clickable.dispatchEvent(new MouseEvent('mouseup', { bubbles: true, cancelable: true, view: window }));
                    clickable.click();
                    if (verify()) return true;
                }

                const preferred = Array.from(document.querySelectorAll('div.value__val')).filter(isVisible);
                let target = preferred.find((el) => timeRegex.test((el.textContent || '').trim())) || preferred[0] || null;

                if (!target) {
                    const fallback = Array.from(document.querySelectorAll('*')).filter(isVisible);
                    target = fallback.find((el) => {
                        const txt = (el.textContent || '').trim();
                        return timeRegex.test(txt) && txt.includes(':');
                    }) || null;
                }

                if (!target) return false;

                target.textContent = value;
                target.innerText = value;
                fireEvents(target);

                return verify() || (target.textContent || '').trim() === value;
                """;

            var success = false;
            for (var attempt = 1; attempt <= 4; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _driver.SwitchTo().DefaultContent();
                var result = js.ExecuteScript(script, expirationText);
                success = result is bool b && b;

                if (!success)
                {
                    var frames = _driver.FindElements(By.CssSelector("iframe, frame"));
                    foreach (var frame in frames)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            _driver.SwitchTo().DefaultContent();
                            _driver.SwitchTo().Frame(frame);
                            result = js.ExecuteScript(script, expirationText);
                            success = result is bool frameResult && frameResult;
                            if (success)
                            {
                                break;
                            }
                        }
                        catch
                        {
                        }
                    }
                }

                _driver.SwitchTo().DefaultContent();
                if (success)
                {
                    break;
                }

                Thread.Sleep(250);
            }

            Log?.Invoke(success
                ? $"Expiration synced to broker: {expirationText}"
                : "Expiration sync failed: target element not found or write rejected.");
            return Task.FromResult(success);
        }
        catch (Exception ex)
        {
            Log?.Invoke($"SetExpiration failed: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    public async Task<TimeSpan?> ReadCurrentExpirationAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_driver is null)
        {
            return null;
        }

        try
        {
            EnsureBrokerPage();
            var js = (IJavaScriptExecutor)_driver;
            var script = """
                const isVisible = (el) => !!(el && (el.offsetWidth || el.offsetHeight || el.getClientRects().length));
                const timeRegex = /^\d{2}:\d{2}:\d{2}$|^\d{1,2}:\d{2}$/;

                const findTradeRoot = () => {
                    const switchItem = document.querySelector('span.switch-state-block__item');
                    if (!switchItem) return null;

                    let current = switchItem;
                    for (let i = 0; i < 8 && current; i++) {
                        const cls = (current.className || '').toString().toLowerCase();
                        if (cls.includes('trade') || cls.includes('trading') || cls.includes('panel') || cls.includes('controls') || cls.includes('right-block')) {
                            return current;
                        }
                        current = current.parentElement;
                    }

                    return switchItem.parentElement;
                };

                const selectors = [
                    'div.value__val',
                    '.value__val',
                    '[class*="expiration"]',
                    '[class*="expiry"]',
                    '[class*="time-picker"]',
                    '[class*="duration"]',
                    '[data-qa*="expiration"]',
                    '[data-qa*="expiry"]',
                    '[data-test*="expiration"]',
                    '[data-test*="expiry"]'
                ];

                const root = findTradeRoot();
                const searchContext = root || document;

                const candidates = Array.from(searchContext.querySelectorAll(selectors.join(',')))
                    .filter(isVisible);

                for (const el of candidates) {
                    const txt = (el.textContent || '').trim();
                    if (timeRegex.test(txt)) {
                        return txt;
                    }
                }

                if (root) {
                    const rootTimes = Array.from(root.querySelectorAll('*'))
                        .filter(isVisible)
                        .map((el) => (el.textContent || '').trim())
                        .filter((txt) => timeRegex.test(txt));

                    if (rootTimes.length) {
                        return rootTimes[0];
                    }
                }

                return null;
                """;

            string? raw = null;
            for (var attempt = 1; attempt <= 3 && string.IsNullOrWhiteSpace(raw); attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _driver.SwitchTo().DefaultContent();
                raw = js.ExecuteScript(script)?.ToString();

                if (string.IsNullOrWhiteSpace(raw))
                {
                    var frames = _driver.FindElements(By.CssSelector("iframe, frame"));
                    foreach (var frame in frames)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            _driver.SwitchTo().DefaultContent();
                            _driver.SwitchTo().Frame(frame);
                            raw = js.ExecuteScript(script)?.ToString();
                            if (!string.IsNullOrWhiteSpace(raw))
                            {
                                break;
                            }
                        }
                        catch
                        {
                        }
                    }
                }

                _driver.SwitchTo().DefaultContent();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    await Task.Delay(150, cancellationToken);
                }
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            if (TimeSpan.TryParseExact(raw.Trim(), "hh\\:mm\\:ss", CultureInfo.InvariantCulture, out var hhmmss))
            {
                return hhmmss;
            }

            if (TimeSpan.TryParseExact(raw.Trim(), "m\\:ss", CultureInfo.InvariantCulture, out var mmss1))
            {
                return mmss1;
            }

            if (TimeSpan.TryParseExact(raw.Trim(), "mm\\:ss", CultureInfo.InvariantCulture, out var mmss2))
            {
                return mmss2;
            }

            return null;
        }
        catch (Exception ex)
        {
            Log?.Invoke($"ReadCurrentExpiration failed: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> ActivateAiTradingAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_driver is null)
        {
            return false;
        }

        try
        {
            EnsureBrokerPage();
            var js = (IJavaScriptExecutor)_driver;
            var script = """
                const isVisible = (el) => !!(el && (el.offsetWidth || el.offsetHeight || el.getClientRects().length));

                const labels = Array.from(document.querySelectorAll('div.ai-trading-btn__text, .ai-trading-btn__text'))
                    .filter(isVisible)
                    .filter((el) => (el.textContent || '').trim().toLowerCase().includes('trading'));

                if (!labels.length) return false;

                const label = labels[0];
                const button = label.closest('.ai-trading-btn, button, [role="button"], div') || label;

                const cls = (button.className || '').toString().toLowerCase();
                const state = (button.getAttribute('aria-pressed') || button.getAttribute('data-state') || '').toLowerCase();
                const alreadyActive = cls.includes('active') || state === 'true' || state === 'active' || state === 'on';

                if (alreadyActive) return true;

                try { button.scrollIntoView({ block: 'center', inline: 'center' }); } catch {}
                button.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true, view: window }));
                button.dispatchEvent(new MouseEvent('mouseup', { bubbles: true, cancelable: true, view: window }));
                button.click();

                const clsAfter = (button.className || '').toString().toLowerCase();
                const stateAfter = (button.getAttribute('aria-pressed') || button.getAttribute('data-state') || '').toLowerCase();
                const activeAfter = clsAfter.includes('active') || stateAfter === 'true' || stateAfter === 'active' || stateAfter === 'on';

                return activeAfter || true;
                """;

            var success = false;
            for (var attempt = 1; attempt <= 4 && !success; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _driver.SwitchTo().DefaultContent();
                var result = js.ExecuteScript(script);
                success = result is bool b && b;

                if (!success)
                {
                    var frames = _driver.FindElements(By.CssSelector("iframe, frame"));
                    foreach (var frame in frames)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            _driver.SwitchTo().DefaultContent();
                            _driver.SwitchTo().Frame(frame);
                            result = js.ExecuteScript(script);
                            success = result is bool frameResult && frameResult;
                            if (success)
                            {
                                break;
                            }
                        }
                        catch
                        {
                        }
                    }
                }

                _driver.SwitchTo().DefaultContent();
                if (!success)
                {
                    await Task.Delay(250, cancellationToken);
                }
            }

            Log?.Invoke(success
                ? "Pocket Option AI Trading activated."
                : "AI Trading activation failed: Trading button was not found.");

            return success;
        }
        catch (Exception ex)
        {
            Log?.Invoke($"ActivateAiTrading failed: {ex.Message}");
            return false;
        }
    }

    public async Task<TradeStatsSnapshot?> ReadTradeStatsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_driver is null)
        {
            return null;
        }

        try
        {
            EnsureBrokerPage();
            var js = (IJavaScriptExecutor)_driver;

            var script = """
                const isVisible = (el) => !!(el && (el.offsetWidth || el.offsetHeight || el.getClientRects().length));

                const parseAmount = (raw) => {
                    if (!raw) return 0;
                    const text = raw.toString().replace(/\s/g, '');
                    const match = text.match(/[-+]?\$?([0-9]+(?:\.[0-9]+)?)/);
                    if (!match) return 0;
                    const val = Number(match[1]);
                    return Number.isFinite(val) ? val : 0;
                };

                const parseProfit = (raw) => {
                    if (!raw) return 0;
                    const text = raw.toString().replace(/\s/g, '');
                    const sign = text.includes('-') ? -1 : 1;
                    const match = text.match(/[-+]?\$?([0-9]+(?:\.[0-9]+)?)/);
                    if (!match) return 0;
                    const val = Number(match[1]);
                    if (!Number.isFinite(val)) return 0;
                    return val * sign;
                };

                let openTrades = 0;
                let closedTrades = 0;
                let wins = 0;
                let losses = 0;

                const containers = Array.from(document.querySelectorAll('div.scrollbar-container.deals-list'));

                for (const container of containers) {
                    if (!isVisible(container)) continue;

                    const noDealsText = (container.querySelector('.no-deals')?.textContent || '').trim().toLowerCase();
                    const items = Array.from(container.querySelectorAll('.deals-list__item'));

                    if (noDealsText.includes('no opened trades')) {
                        continue;
                    }

                    const isClosedList = !!container.querySelector('.deals-list__group-label');
                    if (!isClosedList) {
                        openTrades += items.length;
                        continue;
                    }

                    for (const item of items) {
                        const rows = item.querySelectorAll('.item-row');
                        const resultRow = rows.length > 1 ? rows[rows.length - 1] : null;
                        if (!resultRow) continue;

                        const cols = resultRow.querySelectorAll('div');
                        if (cols.length < 3) continue;

                        const stake = parseAmount(cols[0].innerText || cols[0].textContent || '');
                        const payout = parseAmount(cols[1].innerText || cols[1].textContent || '');
                        const profit = parseProfit(cols[2].innerText || cols[2].textContent || '');

                        const isWin = profit > 0 || (payout > stake && payout > 0);

                        closedTrades += 1;
                        if (isWin) {
                            wins += 1;
                        } else {
                            losses += 1;
                        }
                    }
                }

                const denominator = wins + losses;
                const winRate = denominator > 0 ? (wins / denominator) * 100 : 0;

                return JSON.stringify({
                    openTrades,
                    closedTrades,
                    wins,
                    losses,
                    winRate
                });
                """;

            string? json = null;
            for (var attempt = 1; attempt <= 4 && string.IsNullOrWhiteSpace(json); attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _driver.SwitchTo().DefaultContent();
                json = js.ExecuteScript(script)?.ToString();

                if (string.IsNullOrWhiteSpace(json))
                {
                    var frames = _driver.FindElements(By.CssSelector("iframe, frame"));
                    foreach (var frame in frames)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            _driver.SwitchTo().DefaultContent();
                            _driver.SwitchTo().Frame(frame);
                            json = js.ExecuteScript(script)?.ToString();
                            if (!string.IsNullOrWhiteSpace(json))
                            {
                                break;
                            }
                        }
                        catch
                        {
                        }
                    }
                }

                _driver.SwitchTo().DefaultContent();
                if (string.IsNullOrWhiteSpace(json))
                {
                    await Task.Delay(250, cancellationToken);
                }
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var openTrades = root.TryGetProperty("openTrades", out var openProp) ? openProp.GetInt32() : 0;
            var closedTrades = root.TryGetProperty("closedTrades", out var closedProp) ? closedProp.GetInt32() : 0;
            var wins = root.TryGetProperty("wins", out var winsProp) ? winsProp.GetInt32() : 0;
            var losses = root.TryGetProperty("losses", out var lossesProp) ? lossesProp.GetInt32() : 0;
            var winRate = root.TryGetProperty("winRate", out var rateProp) ? rateProp.GetDecimal() : 0m;

            return new TradeStatsSnapshot(openTrades, closedTrades, wins, losses, winRate);
        }
        catch (Exception ex)
        {
            Log?.Invoke($"ReadTradeStats failed: {ex.Message}");
            return null;
        }
    }

    public async Task<AccountMode> ReadActiveAccountModeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_driver is null)
        {
            return AccountMode.Unknown;
        }

        try
        {
            EnsureBrokerPage();
            var js = (IJavaScriptExecutor)_driver;
            var script = """
                const isVisible = (el) => !!(el && (el.offsetWidth || el.offsetHeight || el.getClientRects().length));

                const labels = Array.from(document.querySelectorAll('div.balance-info-block__label')).filter(isVisible);
                if (!labels.length) return 'unknown';

                const entries = labels.map((label) => {
                    const text = (label.textContent || '').trim().toLowerCase();
                    const block = label.closest('.balance-info-block') || label.parentElement;
                    const classText = ((block?.className || '') + ' ' + (label.className || '')).toLowerCase();
                    const pressed = (block?.getAttribute?.('aria-pressed') || '').toLowerCase();
                    const state = (block?.getAttribute?.('data-state') || '').toLowerCase();
                    const isActive = classText.includes('active') || classText.includes('selected') || classText.includes('current')
                        || pressed === 'true' || state === 'active' || state === 'selected' || state === 'on';
                    return { text, isActive };
                });

                const activeEntry = entries.find((x) => x.isActive) || entries[0];
                if (!activeEntry) return 'unknown';

                if (activeEntry.text.includes('demo')) return 'demo';
                if (activeEntry.text.includes('real')) return 'real';
                return 'unknown';
                """;

            string? modeText = null;
            for (var attempt = 1; attempt <= 3 && string.IsNullOrWhiteSpace(modeText); attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _driver.SwitchTo().DefaultContent();
                modeText = js.ExecuteScript(script)?.ToString();

                if (string.IsNullOrWhiteSpace(modeText) || modeText.Equals("unknown", StringComparison.OrdinalIgnoreCase))
                {
                    var frames = _driver.FindElements(By.CssSelector("iframe, frame"));
                    foreach (var frame in frames)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            _driver.SwitchTo().DefaultContent();
                            _driver.SwitchTo().Frame(frame);
                            modeText = js.ExecuteScript(script)?.ToString();
                            if (!string.IsNullOrWhiteSpace(modeText) && !modeText.Equals("unknown", StringComparison.OrdinalIgnoreCase))
                            {
                                break;
                            }
                        }
                        catch
                        {
                        }
                    }
                }

                _driver.SwitchTo().DefaultContent();
                if (string.IsNullOrWhiteSpace(modeText))
                {
                    await Task.Delay(200, cancellationToken);
                }
            }

            return modeText?.Trim().ToLowerInvariant() switch
            {
                "demo" => AccountMode.Demo,
                "real" => AccountMode.Real,
                _ => AccountMode.Unknown
            };
        }
        catch (Exception ex)
        {
            Log?.Invoke($"ReadActiveAccountMode failed: {ex.Message}");
            return AccountMode.Unknown;
        }
    }

    public async Task<bool> PlaceTradeAsync(string direction, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_driver is null)
        {
            return false;
        }

        try
        {
            var normalizedDirection = (direction ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedDirection != "buy" && normalizedDirection != "sell")
            {
                normalizedDirection = "buy";
            }
            var success = false;
            for (var attempt = 1; attempt <= 4 && !success; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _driver.SwitchTo().DefaultContent();
                success = await TryClickTradeInCurrentContextAsync(normalizedDirection, cancellationToken);

                if (!success)
                {
                    var frames = _driver.FindElements(By.CssSelector("iframe, frame"));
                    foreach (var frame in frames)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            _driver.SwitchTo().DefaultContent();
                            _driver.SwitchTo().Frame(frame);
                            success = await TryClickTradeInCurrentContextAsync(normalizedDirection, cancellationToken);
                            if (success)
                            {
                                break;
                            }
                        }
                        catch
                        {
                        }
                    }
                }

                _driver.SwitchTo().DefaultContent();
                if (!success)
                {
                    await Task.Delay(250, cancellationToken);
                }
            }

            Log?.Invoke(success
                ? $"Trade click executed on broker: {normalizedDirection.ToUpperInvariant()}"
                : $"Trade click failed: {normalizedDirection.ToUpperInvariant()} button was not found.");

            return success;
        }
        catch (Exception ex)
        {
            Log?.Invoke($"PlaceTrade failed: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> TryClickTradeInCurrentContextAsync(string direction, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_driver is null)
        {
            return false;
        }

        var token = direction == "sell" ? "btn-sell" : "btn-buy";
        var label = direction == "sell" ? "sell" : "buy";

        if (TryClickViaJavaScript(direction))
        {
            return true;
        }

        var svgMatches = _driver.FindElements(By.CssSelector($"svg[data-src*='{token}']"));
        foreach (var svg in svgMatches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryNativeClick(svg) || TryClickClosestClickable(svg))
            {
                return true;
            }
        }

        var textTargets = _driver.FindElements(By.XPath($"//*[contains(translate(normalize-space(text()), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), '{label}')]")).Take(40);
        foreach (var element in textTargets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryNativeClick(element) || TryClickClosestClickable(element))
            {
                return true;
            }
        }

        await Task.Delay(50, cancellationToken);
        return false;
    }

    private bool TryClickViaJavaScript(string direction)
    {
        if (_driver is not IJavaScriptExecutor js)
        {
            return false;
        }

        var script = """
            const direction = (arguments[0] || 'buy').toString().trim().toLowerCase();
            const token = direction === 'sell' ? 'btn-sell' : 'btn-buy';
            const label = direction === 'sell' ? 'sell' : 'buy';

            const bySvg = Array.from(document.querySelectorAll(`svg[data-src*="${token}"]`));
            for (const svg of bySvg) {
                const clickable = svg.closest('span.switch-state-block__item, button, [role="button"]') || svg.parentElement;
                if (clickable) {
                    try { clickable.scrollIntoView({ block: 'center', inline: 'center' }); } catch {}
                    clickable.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true, view: window }));
                    clickable.dispatchEvent(new MouseEvent('mouseup', { bubbles: true, cancelable: true, view: window }));
                    clickable.click();
                    return true;
                }
            }

            const byText = Array.from(document.querySelectorAll('span.switch-state-block__item, span.payout__text, button, [role="button"]'));
            for (const el of byText) {
                const txt = (el.textContent || '').trim().toLowerCase();
                if (!txt.includes(label)) continue;
                const clickable = el.closest('span.switch-state-block__item, button, [role="button"]') || el;
                try { clickable.scrollIntoView({ block: 'center', inline: 'center' }); } catch {}
                clickable.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true, view: window }));
                clickable.dispatchEvent(new MouseEvent('mouseup', { bubbles: true, cancelable: true, view: window }));
                clickable.click();
                return true;
            }

            return false;
            """;

        var result = js.ExecuteScript(script, direction);
        return result is bool b && b;
    }

    private bool TryClickClosestClickable(IWebElement source)
    {
        if (_driver is not IJavaScriptExecutor js)
        {
            return false;
        }

        try
        {
            var closest = js.ExecuteScript(
                "return arguments[0].closest('span.switch-state-block__item, button, [role=\"button\"]') || arguments[0].parentElement;",
                source) as IWebElement;

            return closest is not null && TryNativeClick(closest);
        }
        catch
        {
            return false;
        }
    }

    private bool TryNativeClick(IWebElement element)
    {
        if (_driver is null || _driver is not IJavaScriptExecutor js)
        {
            return false;
        }

        try
        {
            js.ExecuteScript("arguments[0].scrollIntoView({block:'center',inline:'center'});", element);
        }
        catch
        {
        }

        try
        {
            element.Click();
            return true;
        }
        catch
        {
            try
            {
                js.ExecuteScript("arguments[0].click();", element);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public void SetPageLoaded(bool value) => IsPageLoaded = value;
    public void SetLoginDetected(bool value) => IsLoginDetected = value;

    private IWebDriver BuildChrome(ProfileMode mode, string profilePath)
    {
        var chromePath = ResolveChromeBinaryPath();
        var debugPort = GetFreeTcpPort();

        var args = new List<string>
        {
            $"--remote-debugging-port={debugPort}",
            "--new-window",
            "--disable-gpu",
            "--disable-background-networking",
            "--disable-component-update",
            "--disable-sync",
            "--disable-notifications",
            "--metrics-recording-only",
            "--no-first-run",
            "--no-default-browser-check",
            "--log-level=3"
        };

        if (mode == ProfileMode.Persistent)
        {
            args.Add($"--user-data-dir=\"{profilePath}\"");
        }

        args.Add(DefaultBrokerUrl);

        var startInfo = new ProcessStartInfo
        {
            FileName = chromePath,
            Arguments = string.Join(' ', args),
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _chromeProcess = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to launch installed Chrome process.");

        WaitForDebugPort(debugPort, TimeSpan.FromSeconds(8));

        var options = new ChromeOptions
        {
            PageLoadStrategy = PageLoadStrategy.Eager,
            DebuggerAddress = $"127.0.0.1:{debugPort}"
        };

        var service = ChromeDriverService.CreateDefaultService();
        service.HideCommandPromptWindow = true;
        service.SuppressInitialDiagnosticInformation = true;

        return new ChromeDriver(service, options, TimeSpan.FromSeconds(20));
    }

    private void EnsureBrokerPage()
    {
        if (_driver is null)
        {
            return;
        }

        try
        {
            var current = _driver.Url;
            if (!string.IsNullOrWhiteSpace(current) && current.Contains("pocketoption.com", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }
        catch
        {
        }

        _driver.Navigate().GoToUrl(DefaultBrokerUrl);
    }

    private static IWebDriver BuildEdge(ProfileMode mode, string profilePath)
    {
        var options = new EdgeOptions();
        options.PageLoadStrategy = PageLoadStrategy.Eager;
        options.AddArgument("--disable-gpu");
        options.AddArgument("--no-default-browser-check");
        if (mode == ProfileMode.Persistent)
        {
            options.AddArgument($"--user-data-dir={profilePath}");
        }

        return new EdgeDriver(options);
    }

    private void CloseBrowserInternal()
    {
        try
        {
            _driver?.Quit();
            _driver?.Dispose();
        }
        catch
        {
        }
        finally
        {
            _driver = null;
            _activeBrowserType = null;
            if (_chromeProcess is { HasExited: false })
            {
                _chromeProcess.Kill(true);
            }
            _chromeProcess?.Dispose();
            _chromeProcess = null;
            IsDriverReady = false;
            IsPageLoaded = false;
            IsLoginDetected = false;
        }
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static void WaitForDebugPort(int port, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            try
            {
                using var client = new TcpClient();
                client.Connect(IPAddress.Loopback, port);
                return;
            }
            catch
            {
                Thread.Sleep(100);
            }
        }

        throw new TimeoutException($"Chrome remote-debug port {port} was not ready in time.");
    }

    private static bool TryParseBalance(string raw, out decimal value)
    {
        var normalized = raw.Replace(",", string.Empty).Replace(" ", string.Empty).Trim();
        return decimal.TryParse(normalized, NumberStyles.Number | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }

    private static string ResolveChromeBinaryPath()
    {
        var registryPath = Registry.GetValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe",
            string.Empty,
            null) as string;

        if (!string.IsNullOrWhiteSpace(registryPath) && File.Exists(registryPath))
        {
            return registryPath;
        }

        var registryPathWow6432 = Registry.GetValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe",
            string.Empty,
            null) as string;

        if (!string.IsNullOrWhiteSpace(registryPathWow6432) && File.Exists(registryPathWow6432))
        {
            return registryPathWow6432;
        }

        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "Application", "chrome.exe")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("Google Chrome (chrome.exe) was not found. Install Chrome or provide a valid binary path.");
    }

    public ValueTask DisposeAsync()
    {
        CloseBrowserInternal();
        return ValueTask.CompletedTask;
    }
}
