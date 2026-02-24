using AustinXPowerBot.Desktop.Services;
using AustinXPowerBot.Desktop.Utils;
using System.Threading.Tasks;
using System.Windows;

namespace AustinXPowerBot.Desktop.ViewModels;

public sealed class ClaimBonusViewModel : ObservableObject
{
    private readonly AppStateService _appState;
    private string _bonusCode = string.Empty;
    private string _selectedBonusType = "Welcome Bonus";
    private string _brokerUid = string.Empty;
    private string _status = "Enter your bonus details, then click Claim Bonus.";
    private const string GenerationLockKey = "claim-bonus-generated";
    private const string GeneratedCodeKey = "claim-bonus-code";
    private const string ClaimLockKey = "claim-bonus-claimed";
    private const string ClaimInfoKey = "claim-bonus-info";
    private bool _isGenerationLocked;
    private bool _isClaimLocked;

    public ClaimBonusViewModel(AppStateService appState)
    {
        _appState = appState;
        ClaimBonusCommand = new RelayCommand(_ => ClaimBonus(), _ => !_isClaimLocked);
        GenerateBonusCodeCommand = new RelayCommand(_ => GenerateBonusCode(), _ => !_isGenerationLocked);
        ResetCommand = new RelayCommand(_ => Reset());

        _ = InitializeGenerationLockAsync();
        _ = InitializeClaimLockAsync();
    }

    public RelayCommand ClaimBonusCommand { get; }
    public RelayCommand GenerateBonusCodeCommand { get; }
    public RelayCommand ResetCommand { get; }

    public string BonusCode
    {
        get => _bonusCode;
        set => SetProperty(ref _bonusCode, value);
    }

    public string SelectedBonusType
    {
        get => _selectedBonusType;
        set => SetProperty(ref _selectedBonusType, value);
    }

    public string BrokerUid
    {
        get => _brokerUid;
        set => SetProperty(ref _brokerUid, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    private void ClaimBonus()
    {
        if (_isClaimLocked)
        {
            Status = "Bonus already claimed for this installation.";
            return;
        }

        var bonusCode = (BonusCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(bonusCode))
        {
            Status = "Bonus code is required.";
            return;
        }

        if (!Services.TelegramBonusCodeValidator.IsValid(bonusCode, out var error))
        {
            Status = error ?? "Bonus code is invalid.";
            return;
        }

        var bonusType = (SelectedBonusType ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(bonusType))
        {
            Status = "Bonus type is required.";
            return;
        }

        var brokerUid = (BrokerUid ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(brokerUid))
        {
            Status = "Broker account UID is required.";
            return;
        }

        Status = $"Bonus claim submitted: {bonusType} • Code {bonusCode} • UID {brokerUid}.";

        // Persist claim lock and info so claim cannot be repeated
        try
        {
            var storage = new LocalStorageService();
            var info = new { Code = bonusCode, Type = bonusType, BrokerUid = brokerUid, ClaimedAtUtc = DateTimeOffset.UtcNow };
            _ = storage.SaveAsync(ClaimInfoKey, info);
            _ = storage.SaveAsync(ClaimLockKey, true);
        }
        catch
        {
        }

        _isClaimLocked = true;
        ClaimBonusCommand.RaiseCanExecuteChanged();
    }

    private void Reset()
    {
        BonusCode = string.Empty;
        SelectedBonusType = "Welcome Bonus";
        BrokerUid = string.Empty;
        Status = "Form reset. Enter bonus details to claim.";
    }

    private void GenerateBonusCode()
    {
        if (_isGenerationLocked)
        {
            Status = "Bonus code generation is locked. Only one code is allowed.";
            return;
        }

        // Simulate code generation (since Telegram bot generation is now removed)
        var value = new Random().Next(100000, 1000000);
        BonusCode = $"AXB-{value}";
        Status = "Bonus code generated and locked for this installation.";

        // Persist lock and the generated code so further generation is disabled
        try
        {
            var storage = new LocalStorageService();
            _ = storage.SaveAsync(GeneratedCodeKey, BonusCode);
            _ = storage.SaveAsync(GenerationLockKey, true);
        }
        catch
        {
        }

        _isGenerationLocked = true;
        GenerateBonusCodeCommand.RaiseCanExecuteChanged();
    }

    private async Task InitializeGenerationLockAsync()
    {
        try
        {
            var storage = new LocalStorageService();
            var locked = await storage.LoadAsync<bool>(GenerationLockKey);
            if (locked)
            {
                var existing = await storage.LoadAsync<string>(GeneratedCodeKey) ?? string.Empty;
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    _isGenerationLocked = true;
                    BonusCode = existing;
                    Status = string.IsNullOrWhiteSpace(existing)
                        ? "Bonus generation locked for this installation."
                        : "A bonus code was previously generated and locked for this installation.";
                    GenerateBonusCodeCommand.RaiseCanExecuteChanged();
                });
            }
        }
        catch
        {
        }
    }

        private async Task InitializeClaimLockAsync()
        {
            try
            {
                var storage = new LocalStorageService();
                var locked = await storage.LoadAsync<bool>(ClaimLockKey);
                if (locked)
                {
                    var info = await storage.LoadAsync<object>(ClaimInfoKey);
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        _isClaimLocked = true;
                        Status = "Bonus has already been claimed for this installation.";
                        ClaimBonusCommand.RaiseCanExecuteChanged();
                    });
                }
            }
            catch
            {
            }
        }
}
