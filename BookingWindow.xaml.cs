using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace WpfApp2
{
    public partial class BookingWindow : Window
    {
        private readonly HttpClient _client;
        private readonly Screening _screening;
        private readonly string _authToken;
        private readonly JsonSerializerSettings _jsonSettings;

        // A konstruktor fogadja a szükséges adatokat a főablakból
        public BookingWindow(HttpClient client, Screening screening, string authToken)
        {
            InitializeComponent();
            _client = client;
            _screening = screening;
            _authToken = authToken;

            _jsonSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            // Az ablak UI-jának feltöltése a kapott adatokkal
            MovieTitleText.Text = _screening.Movie?.Title ?? "Ismeretlen Film";
            ScreeningTimeText.Text = _screening.Time.ToString("yyyy. MMMM dd., HH:mm");
            RoomText.Text = $"Terem: {_screening.Room}";
            DescriptionText.Text = _screening.Movie?.Description ?? "Nincs leírás.";
        }

        private async void ConfirmBookingButton_Click(object sender, RoutedEventArgs e)
        {
            string seat = SeatInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(seat))
            {
                MessageBox.Show("Kérjük, adja meg a lefoglalni kívánt helyet!", "Hiányzó adat", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var payload = new BookingPayload { ScreeningId = _screening.Id, Seat = seat };
            var jsonPayload = JsonConvert.SerializeObject(payload, _jsonSettings);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            // A token hozzáadása a request headerhez
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/bookings")
            {
                Content = content
            };
            request.Headers.Add("x-auth-token", _authToken);

            try
            {
                ConfirmBookingButton.IsEnabled = false;
                ConfirmBookingButton.Content = "Foglalás...";

                var response = await _client.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show($"Sikeresen lefoglalta a(z) '{seat}' helyet!", "Sikeres foglalás", MessageBoxButton.OK, MessageBoxImage.Information);
                    this.DialogResult = true; // Jelzi a főablaknak, hogy a művelet sikeres volt
                    this.Close();
                }
                else
                {
                    var errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(responseContent, _jsonSettings);
                    MessageBox.Show(errorResponse?.Message ?? $"Hiba: {response.StatusCode}", "Foglalási hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kivétel történt a foglalás során: {ex.Message}", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ConfirmBookingButton.IsEnabled = true;
                ConfirmBookingButton.Content = "Hely lefoglalása";
            }
        }
    }
}