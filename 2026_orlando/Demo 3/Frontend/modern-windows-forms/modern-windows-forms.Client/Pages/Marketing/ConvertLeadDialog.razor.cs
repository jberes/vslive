using IgniteUI.Blazor.Controls;
using Microsoft.AspNetCore.Components;
using modern_windows_forms.Client.Models;
using modern_windows_forms.Client.Models.Marketing;
using modern_windows_forms.Client.Services;

namespace modern_windows_forms.Client.Pages.Marketing;

public partial class ConvertLeadDialog
{
    [Inject] private ApiClient Api { get; set; } = default!;
    [Inject] private ILogger<ConvertLeadDialog> Logger { get; set; } = default!;

    /// <summary>Raised after a successful conversion (with any edits applied).</summary>
    [Parameter] public EventCallback<LeadConversion> Converted { get; set; }

    /// <summary>Raised when the dialog closes without converting, so the board can put the card back.</summary>
    [Parameter] public EventCallback<LeadDto> Cancelled { get; set; }

    private IgbDialog? dialog;
    private LeadDto? lead;
    private bool converted;
    private bool busy;
    private ProblemDetails? problem;

    private string companyName = "";
    private string code = "";
    private string country = "";
    private string contactName = "";
    private string contactTitle = "";
    private bool keepOwner = true;

    private List<string> countryOptions = [];

    public async Task OpenAsync(LeadDto target)
    {
        lead = target;
        converted = false;
        problem = null;
        companyName = target.Company;
        code = "";
        country = target.Country ?? "";
        contactName = target.FullName;
        contactTitle = target.Title ?? "";
        keepOwner = true;

        if (countryOptions.Count == 0)
        {
            try
            {
                var countries = await Api.GetAsync<List<CountryCount>>("api/customers/countries");
                countryOptions = countries.Select(c => c.Country).OrderBy(c => c).ToList();
            }
            catch (ApiException ex)
            {
                Logger.LogWarning("Countries unavailable: {Title}", ex.Problem.Title);
            }
        }
        if (!string.IsNullOrEmpty(country) && !countryOptions.Contains(country))
            countryOptions = [.. countryOptions, country];

        StateHasChanged();
        if (dialog is not null) await dialog.ShowAsync();
    }

    private async Task ConvertAsync()
    {
        if (lead is null) return;
        busy = true;
        problem = null;
        try
        {
            var result = await Api.PostAsync<LeadConversion>($"api/leads/{lead.Id}/convert");
            var c = result.Customer;

            // The convert endpoint copies the lead as-is; apply anything the user changed in the dialog.
            var edited = !string.IsNullOrWhiteSpace(code) || companyName != c.CompanyName || contactName != c.ContactName
                         || contactTitle != (c.ContactTitle ?? "") || country != (c.Country ?? "");
            if (edited)
            {
                var input = new CustomerInput(string.IsNullOrWhiteSpace(code) ? c.Code : code.Trim().ToUpperInvariant(),
                    companyName.Trim(), contactName, contactTitle, c.Email, c.Address, c.City, c.Region, c.PostalCode,
                    country, c.Phone, c.Fax);
                try
                {
                    c = await Api.PutAsync<ConvertedCustomer>($"api/customers/{c.Id}", input);
                }
                catch (ApiException ex)
                {
                    // The lead is already converted; keep it and tell the user which edit was rejected.
                    Logger.LogInformation("Customer edits rejected: {Title}", ex.Problem.Title);
                    problem = ex.Problem;
                }
            }

            converted = true;
            await Converted.InvokeAsync(result with { Customer = c });
            if (problem is null && dialog is not null) await dialog.HideAsync();
        }
        catch (ApiException ex)
        {
            problem = ex.Problem;
        }
        finally
        {
            busy = false;
        }
    }

    private async Task CancelAsync()
    {
        if (dialog is not null) await dialog.HideAsync();
    }

    private async Task OnClosed(IgbVoidEventArgs _)
    {
        if (!converted && lead is not null) await Cancelled.InvokeAsync(lead);
        lead = null;
        StateHasChanged();
    }
}
