using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Reflection;
using VerafinViewer.Models;
using VerafinViewer.Services;

namespace VerafinViewer.Components.Pages
{
    public partial class Home
    {
        [CascadingParameter]
        private Task<AuthenticationState> AuthenticationStateTask { get; set; } = null!;

        [Inject] private ILogService? LogService { get; set; }

        [Inject] private NavigationManager? NavManager { get; set; }

        [Inject] private IDataService? DataService { get; set; }


        public List<FilesProcessedDto>? ProcessedList { get; set; } = [];

        private bool ShowLoader { get; set; }

        private string? AppUser { get; set; }

        protected override async Task OnInitializedAsync()
        {
            var authState = await AuthenticationStateTask;
            AppUser = authState.User.Identity?.Name?.Split('\\').Last();

            await LoadData();
        }

        private async Task LoadData()
        {
            try
            {
                ShowLoader = true;
                var data = await DataService?.DailyFilesProcessed(AppUser)!;

                if (data != null)
                {
                    ProcessedList = data;
                }

                ShowLoader = false;

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
