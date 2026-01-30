using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;
using System.Reflection;
using VerafinViewer.Models;
using VerafinViewer.Services;

namespace VerafinViewer.Components.Pages
{
    public partial class Logs
    {
        [CascadingParameter]
        private Task<AuthenticationState> AuthenticationStateTask { get; set; } = null!;

        [Inject] private ILogService? LogService { get; set; }

        [Inject] private NavigationManager? NavManager { get; set; }

        [Inject] private IDataService? DataService { get; set; }

        [Inject] private IOptions<AppSettings>? Settings { get; set; }

        private List<LogsDto>? LogData { get; set; } = [];

        private bool ShowLoader { get; set; }

        private string? AppUser { get; set; }

        private DateTime StartDate { get; set; }
        private DateTime EndDate { get; set; }

        protected override async Task OnInitializedAsync()
        {
            var authState = await AuthenticationStateTask;
            AppUser = authState.User.Identity?.Name?.Split('\\').Last();

            EndDate = DateTime.Now;
            StartDate = EndDate.AddDays(-Settings!.Value.DefaultDateRange);

            await LoadData();
        }

        private async Task LoadData()
        {
            try
            {
                ShowLoader = true;

                var startDate = DateTime.Parse($"{StartDate.ToShortDateString()} 00:00:00");
                var endDate = DateTime.Parse($"{EndDate.ToShortDateString()} 23:59:59");
                var data = await DataService?.GetLogs(new DateParameters
                {
                    EndDate = endDate,
                    StartDate = startDate
                }, AppUser)!;

                if (data != null)
                {
                    LogData = data;
                }

                ShowLoader = false;
                StateHasChanged();

            }
            catch (Exception ex)
            {
                await LogService?.LogAlert(AppLogPrep.AppLogSetup(AppUser, NavManager?.Uri!,
                    MethodName.GetMethodName(MethodBase.GetCurrentMethod()), ex))!;

                NavManager?.NavigateTo("./error");
            }
        }

        private async Task StartDateChanged(DateTime date)
        {
            try
            {
                StartDate = date;
                await LoadData();

            }
            catch (Exception ex)
            {
                await LogService?.LogAlert(AppLogPrep.AppLogSetup(AppUser, NavManager?.Uri!,
                    MethodName.GetMethodName(MethodBase.GetCurrentMethod()), ex))!;

                NavManager?.NavigateTo("./error");
            }
        }

        private async Task EndDateChanged(DateTime date)
        {
            try
            {
                EndDate = date;
                await LoadData();

            }
            catch (Exception ex)
            {
                await LogService?.LogAlert(AppLogPrep.AppLogSetup(AppUser, NavManager?.Uri!,
                    MethodName.GetMethodName(MethodBase.GetCurrentMethod()), ex))!;

                NavManager?.NavigateTo("./error");
            }
        }
    }
}
