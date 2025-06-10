using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace WpfApp2
{
    public partial class ProfileWindow : Window
    {
        private readonly HttpClient _client;
        private readonly User _currentUser;
        private readonly string _authToken;
        private readonly JsonSerializerSettings _jsonSettings;

        public ProfileWindow(HttpClient client, User currentUser, string authToken)
        {
            InitializeComponent();
            _client = client;
            _currentUser = currentUser;
            _authToken = authToken;

            _jsonSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            UserInfoText.Text = $"Felhasználó: {_currentUser.Username} ({_currentUser.EmailAddress})";
        }

        // Amikor az ablak betöltődik, automatikusan lekérjük a foglalásokat
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadUserBookingsAsync();
            await LoadUserStatsAsync();
        }

        private async Task LoadUserBookingsAsync()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/users/me/bookings");
            request.Headers.Add("x-auth-token", _authToken);

            try
            {
                var response = await _client.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var bookings = JsonConvert.DeserializeObject<List<Booking>>(json, _jsonSettings);
                    BookingList.ItemsSource = bookings;
                }
                else
                {
                    MessageBox.Show($"Hiba a foglalások lekérésekor: {json}", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kivétel történt a foglalások lekérésekor: {ex.Message}", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        private async Task LoadUserStatsAsync()
        {
            try
            {
                var response = await _client.GetAsync($"/api/users/{_currentUser.Id}/stats");
                var json = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var stats = JsonConvert.DeserializeObject<UserStats>(json, _jsonSettings);
                    UserStatsText.Text = $"Összes foglalás: {stats.totalBookings}";
                }
                else
                {
                    UserStatsText.Text = "Nem sikerült betölteni a statisztikát.";
                }
            }
            catch (Exception ex)
            {
                UserStatsText.Text = $"Hiba történt: {ex.Message}";
            }
        }





        private async void DeleteBookingButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is int bookingId)
            {
                if (MessageBox.Show("Biztosan törölni szeretnéd ezt a foglalást?", "Megerősítés", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                {
                    return;
                }

                var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/bookings/{bookingId}");
                request.Headers.Add("x-auth-token", _authToken);

                try
                {
                    var response = await _client.SendAsync(request);
                    var json = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        MessageBox.Show("Foglalás sikeresen törölve.", "Siker", MessageBoxButton.OK, MessageBoxImage.Information);
                        await LoadUserBookingsAsync(); // Lista frissítése
                        await LoadUserStatsAsync();
                    }
                    else
                    {
                        var errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(json, _jsonSettings);
                        MessageBox.Show(errorResponse?.Message ?? "Hiba a törlés során.", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Kivétel történt a törlés során: {ex.Message}", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }


    public class UserStats
    {
        public int totalBookings { get; set; }
    }
}