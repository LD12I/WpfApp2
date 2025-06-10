using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace WpfApp2
{
    public partial class MainWindow : Window
    {
        #region Mezők és Tulajdonságok

        private readonly HttpClient client = new HttpClient { BaseAddress = new Uri("http://localhost:4444") };
        private List<Movie> allMoviesCache = new List<Movie>();
        private List<Screening> allScreeningsCache = new List<Screening>();
        private readonly JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        };
        private string authToken = null;
        private User currentUser = null;
        private int? editingMovieId = null;

        #endregion

        #region Inicializálás

        public MainWindow()
        {
            InitializeComponent();
            InitializeApplicationState();
        }

        private async void InitializeApplicationState()
        {
            UpdateUIVisibility();
            RegisterPanel.Visibility = Visibility.Collapsed;
            AddMoviePanel.Visibility = Visibility.Collapsed;
            AddScreeningPanel.Visibility = Visibility.Collapsed;
            CancelEditButton.Visibility = Visibility.Collapsed;
            await RefreshAllDataAsync();
        }

        #endregion

        #region UI és Adatkezelés

        private void UpdateUIVisibility()
        {
            bool isLoggedIn = currentUser != null;
            bool isAdmin = isLoggedIn && currentUser.IsAdmin;

            LoginPanel.Visibility = isLoggedIn ? Visibility.Collapsed : Visibility.Visible;
            if (RegisterPanel.Visibility == Visibility.Visible && isLoggedIn)
            {
                RegisterPanel.Visibility = Visibility.Collapsed;
            }
            UserInfoPanel.Visibility = isLoggedIn ? Visibility.Visible : Visibility.Collapsed;
            if (isLoggedIn)
            {
                LoggedInUserText.Text = $"Bejelentkezve: {currentUser.Username}";
            }

            if (!isAdmin)
            {
                if (AddMoviePanel.Visibility == Visibility.Visible) CancelEditMode();
                if (AddScreeningPanel.Visibility == Visibility.Visible) CancelAddScreeningButton_Click(null, null);
            }
            AddNewMovieButton.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            AddNewScreeningButton.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
        }

        private async Task RefreshAllDataAsync()
        {
            await Task.WhenAll(LoadMoviesAsync(), LoadScreeningsAsync());
            PopulateScreeningMovieTitles();
            UpdateMovieUI();
            UpdateScreeningUI();
            LoadTopMoviesAsync();
        }

        async Task LoadTopMoviesAsync()
        {
            try
            {
                var res = await client.GetAsync("/api/movies/top");
                var json = await res.Content.ReadAsStringAsync();

                if (res.IsSuccessStatusCode)
                {
                    var topMovies = JsonConvert.DeserializeObject<List<TopMovie>>(json, jsonSerializerSettings);

                    TopMoviesStackpanel.Children.Clear();

                    foreach (var movie in topMovies)
                    {
                        var tb = new TextBlock
                        {
                            Text = $"{movie.Title} - Foglalások száma: {movie.totalBookings}",
                            Margin = new Thickness(5),
                            FontWeight = FontWeights.Bold
                        };
                        TopMoviesStackpanel.Children.Add(tb);
                    }
                }
                else
                {
                    MessageBox.Show($"Nem sikerült lekérni a top filmeket: {json}", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hiba történt a top filmek lekérésekor: {ex.Message}", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        public async Task LoadScreeningStatsAsync(int id)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/screenings/{id}/stats");

            try
            {
                var response = await client.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var stats = JsonConvert.DeserializeObject<ScreeningStats>(json);

                    ScreeningStatsText.Text = $"Foglalt helyek: {stats.totalSeats} / {stats.bookingCount} " +
                        $"({stats.percentage}%)";
                }
                else
                {
                    ScreeningStatsText.Text = "Nem sikerült betölteni a statisztikát.";
                }
            }
            catch (Exception ex)
            {
                ScreeningStatsText.Text = $"Hiba történt: {ex.Message}";
            }
        }


        private async Task LoadMoviesAsync()
        {
            try
            {
                var res = await client.GetAsync("/api/movies/movies");
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync();
                    allMoviesCache = JsonConvert.DeserializeObject<List<Movie>>(json, jsonSerializerSettings) ?? new List<Movie>();
                }
                else
                {
                    MessageBox.Show($"Filmek betöltése sikertelen: {res.StatusCode} - {await res.Content.ReadAsStringAsync()}", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                    allMoviesCache.Clear();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kivétel a filmek betöltése során: {ex.Message}", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                allMoviesCache.Clear();
            }
        }

        private async Task LoadScreeningsAsync()
        {
            try
            {
                var res = await client.GetAsync("/api/screenings/screenings");
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync();
                    allScreeningsCache = JsonConvert.DeserializeObject<List<Screening>>(json, jsonSerializerSettings) ?? new List<Screening>();
                }
                else
                {
                    MessageBox.Show($"Vetítések betöltése sikertelen: {res.StatusCode} - {await res.Content.ReadAsStringAsync()}", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                    allScreeningsCache.Clear();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kivétel a vetítések betöltése során: {ex.Message}", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                allScreeningsCache.Clear();
            }
        }

        private void PopulateScreeningMovieTitles()
        {
            foreach (var screening in allScreeningsCache)
            {
                var movie = allMoviesCache.FirstOrDefault(m => m.Id == screening.MovieId);
                screening.MovieTitle = movie?.Title ?? "Ismeretlen Film";
            }
        }

        private void UpdateMovieUI()
        {
            MovieList.ItemsSource = null;
            MovieList.ItemsSource = allMoviesCache;
            if (SearchInput != null) SearchInput.Text = "";

            var filterMovies = new List<Movie> { new Movie { Id = 0, Title = "Összes film" } };
            filterMovies.AddRange(allMoviesCache);
            ScreeningFilterComboBox.ItemsSource = filterMovies;
            ScreeningFilterComboBox.SelectedIndex = 0;

            ScreeningMovieComboBox.ItemsSource = allMoviesCache;
        }

        private void UpdateScreeningUI(List<Screening> screeningsToDisplay = null)
        {
            ScreeningList.ItemsSource = null;
            ScreeningList.ItemsSource = screeningsToDisplay ?? allScreeningsCache.OrderBy(s => s.Time).ToList();
        }

        #endregion

        #region Felhasználói Műveletek (Login, Register, Logout, Profile)

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            var loginRequest = new LoginRequest { EmailAddress = EmailLoginInput.Text, Password = PasswordLoginInput.Password };
            var jsonPayload = JsonConvert.SerializeObject(loginRequest, jsonSerializerSettings);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            try
            {
                var res = await client.PostAsync("/api/users/loginCheck", content);
                var responseJson = await res.Content.ReadAsStringAsync();
                if (res.IsSuccessStatusCode)
                {
                    var loginResponse = JsonConvert.DeserializeObject<LoginResponse>(responseJson, jsonSerializerSettings);
                    if (loginResponse.Success && loginResponse.User != null && loginResponse.User.Id > 0)
                    {
                        authToken = loginResponse.Token;
                        currentUser = loginResponse.User;
                        MessageBox.Show(loginResponse.Message, "Sikeres bejelentkezés", MessageBoxButton.OK, MessageBoxImage.Information);
                        UpdateUIVisibility();
                        EmailLoginInput.Text = ""; PasswordLoginInput.Password = "";
                        await RefreshAllDataAsync();
                    }
                    else
                    {
                        string errMsg = loginResponse?.Message ?? "Ismeretlen hiba.";
                        MessageBox.Show(errMsg, "Bejelentkezési hiba", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    var errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(responseJson, jsonSerializerSettings);
                    MessageBox.Show(errorResponse?.Message ?? $"Hiba: {res.StatusCode}", "Bejelentkezési hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kivétel a bejelentkezés során: {ex.Message}", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            if (PasswordRegisterInput.Password != PasswordConfirmRegisterInput.Password)
            {
                MessageBox.Show("A megadott jelszavak nem egyeznek!", "Regisztrációs hiba", MessageBoxButton.OK, MessageBoxImage.Warning); return;
            }
            var registerRequest = new RegisterRequest { Username = UsernameRegisterInput.Text, EmailAddress = EmailRegisterInput.Text, Password = PasswordRegisterInput.Password };
            var jsonPayload = JsonConvert.SerializeObject(registerRequest, jsonSerializerSettings);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            try
            {
                var res = await client.PostAsync("/api/users/register", content);
                var responseJson = await res.Content.ReadAsStringAsync();
                if (res.IsSuccessStatusCode)
                {
                    var registerResponse = JsonConvert.DeserializeObject<RegisterResponse>(responseJson, jsonSerializerSettings);
                    if (registerResponse.Success)
                    {
                        MessageBox.Show(registerResponse.Message ?? "Sikeres regisztráció!", "Regisztráció", MessageBoxButton.OK, MessageBoxImage.Information);
                        UsernameRegisterInput.Text = ""; EmailRegisterInput.Text = ""; PasswordRegisterInput.Password = ""; PasswordConfirmRegisterInput.Password = "";
                        ShowLoginButton_Click(null, null);
                    }
                    else
                    {
                        string errorMessage = registerResponse.Message;
                        if (registerResponse.Messages != null && registerResponse.Messages.Count > 0) errorMessage = string.Join(Environment.NewLine, registerResponse.Messages);
                        MessageBox.Show(errorMessage ?? "Ismeretlen hiba.", "Regisztrációs hiba", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    var errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(responseJson, jsonSerializerSettings);
                    string errorMessage = errorResponse?.Message;
                    if (errorResponse?.Messages != null && errorResponse.Messages.Count > 0) errorMessage = string.Join(Environment.NewLine, errorResponse.Messages);
                    MessageBox.Show(errorMessage ?? $"Hiba: {res.StatusCode}", "Regisztrációs hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kivétel a regisztráció során: {ex.Message}", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            authToken = null;
            currentUser = null;
            client.DefaultRequestHeaders.Authorization = null;
            UpdateUIVisibility();
            ClearMovieDetails();
            MessageBox.Show("Sikeresen kijelentkeztél.", "Kijelentkezés", MessageBoxButton.OK, MessageBoxImage.Information);
            await RefreshAllDataAsync();
            CancelEditMode();
            CancelAddScreeningButton_Click(null, null);
        }

        // ÚJ ESEMÉNYKEZELŐ
        private void ProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentUser == null)
            {
                MessageBox.Show("A profil megtekintéséhez be kell jelentkeznie.", "Hiba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var profileWindow = new ProfileWindow(client, currentUser, authToken)
            {
                Owner = this // Beállítjuk a főablakot tulajdonosnak
            };
            profileWindow.ShowDialog();
        }

        #endregion

        #region Film Műveletek (CRUD)

        private async void MovieList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MovieList.SelectedItem is Movie selectedMovie)
            {
                if (selectedMovie.Id <= 0) { ClearMovieDetails(); return; }
                string requestUrl = $"/api/movies/movie-by-id/{selectedMovie.Id}";
                try
                {
                    var res = await client.GetAsync(requestUrl);
                    var responseContent = await res.Content.ReadAsStringAsync();
                    if (res.IsSuccessStatusCode)
                    {
                        var movie = JsonConvert.DeserializeObject<Movie>(responseContent, jsonSerializerSettings);
                        if (movie != null) { TitleText.Text = movie.Title; YearText.Text = movie.Year.ToString(); DescriptionText.Text = movie.Description; }
                    }
                }
                catch (Exception ex) { MessageBox.Show($"Kivétel film lekérésekor: {ex.Message}", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error); ClearMovieDetails(); }
            }
            else { ClearMovieDetails(); }
        }

        private void ClearMovieDetails()
        {
            if (TitleText != null) TitleText.Text = "Nincs film kiválasztva";
            if (YearText != null) YearText.Text = "";
            if (DescriptionText != null) DescriptionText.Text = "";
        }

        private async void CreateOrUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentUser == null || !currentUser.IsAdmin) { MessageBox.Show("Nincs jogosultságod!", "Hiba", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            var title = TitleInput.Text; var description = DescriptionInput.Text; var imgUrl = ImgInput.Text;
            if (!int.TryParse(YearInput.Text, out int year) || year < 1800 || year > DateTime.Now.Year + 10)
            { MessageBox.Show("Érvénytelen év.", "Hiba", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description) || string.IsNullOrWhiteSpace(imgUrl))
            { MessageBox.Show("Minden mezőt ki kell tölteni.", "Hiányzó adatok", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            var moviePayload = new MoviePayload { Title = title, Description = description, Year = year, Img = imgUrl, AccountId = currentUser.Id };
            var jsonPayload = JsonConvert.SerializeObject(moviePayload, jsonSerializerSettings);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                var request = new HttpRequestMessage() { Content = content };
                request.Headers.Add("x-auth-token", authToken); // Token hozzáadása
                string successMessage;

                if (editingMovieId.HasValue)
                {
                    request.Method = HttpMethod.Put;
                    request.RequestUri = new Uri($"/api/movies/movies/{editingMovieId.Value}", UriKind.Relative);
                    successMessage = "Film sikeresen frissítve!";
                }
                else
                {
                    request.Method = HttpMethod.Post;
                    request.RequestUri = new Uri("/api/movies/movies", UriKind.Relative);
                    successMessage = "Film sikeresen létrehozva!";
                }

                var res = await client.SendAsync(request);

                if (res.IsSuccessStatusCode)
                {
                    MessageBox.Show(successMessage, "Siker", MessageBoxButton.OK, MessageBoxImage.Information);
                    CancelEditMode();
                    await RefreshAllDataAsync();
                }
                else
                {
                    var errorContent = await res.Content.ReadAsStringAsync();
                    MessageBox.Show($"Művelet sikertelen: {res.StatusCode} - {errorContent}", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kivétel a művelet során: {ex.Message}", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditMovieButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentUser == null || !currentUser.IsAdmin)
            { MessageBox.Show("Nincs jogosultságod!", "Hiba", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            if (sender is Button button && button.DataContext is Movie movieToEdit)
            {
                SetEditMode(movieToEdit);
            }
        }

        private async void DeleteMovieButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentUser == null || !currentUser.IsAdmin)
            { MessageBox.Show("Nincs jogosultságod!", "Hiba", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            if (sender is Button button && button.DataContext is Movie movieToDelete)
            {
                if (MessageBox.Show($"Biztosan törlöd a '{movieToDelete.Title}' filmet és minden hozzá tartozó vetítést?", "Megerősítés", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    var deleteRequestPayload = new MovieDeleteRequest { AccountId = currentUser.Id };
                    var jsonPayload = JsonConvert.SerializeObject(deleteRequestPayload, jsonSerializerSettings);
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                    var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/movies/movies/{movieToDelete.Id}") { Content = content };
                    request.Headers.Add("x-auth-token", authToken); // Token hozzáadása
                    try
                    {
                        var res = await client.SendAsync(request);
                        if (res.IsSuccessStatusCode)
                        {
                            MessageBox.Show("Film sikeresen törölve!");
                            await RefreshAllDataAsync();
                        }
                        else
                        {
                            var errorContent = await res.Content.ReadAsStringAsync();
                            MessageBox.Show($"Film törlése sikertelen: {res.StatusCode} - {errorContent}", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex) { MessageBox.Show($"Kivétel a film törlése során: {ex.Message}", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error); }
                }
            }
        }

        private void SetEditMode(Movie movieToEdit = null)
        {
            if (currentUser == null || !currentUser.IsAdmin)
            {
                MessageBox.Show("Nincs jogosultságod ehhez a művelethez.", "Hiba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AddMoviePanel.Visibility = Visibility.Visible;
            CancelEditButton.Visibility = Visibility.Visible;

            if (movieToEdit != null)
            {
                editingMovieId = movieToEdit.Id;
                AddMoviePanelTitle.Text = "Film szerkesztése";
                CreateOrUpdateButton.Content = "Módosítások mentése";
                TitleInput.Text = movieToEdit.Title;
                YearInput.Text = movieToEdit.Year.ToString();
                DescriptionInput.Text = movieToEdit.Description;
                ImgInput.Text = movieToEdit.Img;
            }
            else
            {
                editingMovieId = null;
                AddMoviePanelTitle.Text = "Új film hozzáadása";
                CreateOrUpdateButton.Content = "Létrehozás";
                TitleInput.Text = "";
                YearInput.Text = "";
                DescriptionInput.Text = "";
                ImgInput.Text = "";
            }
            AddMoviePanel.BringIntoView();
        }

        private void CancelEditMode()
        {
            editingMovieId = null;
            AddMoviePanel.Visibility = Visibility.Collapsed;
            CancelEditButton.Visibility = Visibility.Collapsed;
        }

        private void AddNewMovieButton_Click(object sender, RoutedEventArgs e)
        {
            SetEditMode(null);
        }

        private void CancelEditButton_Click(object sender, RoutedEventArgs e)
        {
            CancelEditMode();
        }

       




        #endregion

        #region Vetítés és Foglalás Műveletek

        private void ScreeningFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ScreeningDatePicker.SelectedDate != null)
            {
                ScreeningDatePicker.SelectedDate = null;
                ClearDateFilterButton.Visibility = Visibility.Collapsed;
            }

            if (ScreeningFilterComboBox.SelectedItem is Movie selectedMovie)
            {
                if (selectedMovie.Id == 0) UpdateScreeningUI();
                else
                {
                    var filteredScreenings = allScreeningsCache.Where(s => s.MovieId == selectedMovie.Id).OrderBy(s => s.Time).ToList();
                    UpdateScreeningUI(filteredScreenings);
                }
            }
        }

        private async void ScreeningDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ScreeningDatePicker.SelectedDate.HasValue)
            {
                if (ScreeningFilterComboBox.SelectedIndex != 0) ScreeningFilterComboBox.SelectedIndex = 0;
                ClearDateFilterButton.Visibility = Visibility.Visible;
                string dateString = ScreeningDatePicker.SelectedDate.Value.ToString("yyyy-MM-dd");
                try
                {
                    var res = await client.GetAsync($"/api/screenings/date/{dateString}");
                    if (res.IsSuccessStatusCode)
                    {
                        var json = await res.Content.ReadAsStringAsync();
                        var filteredScreenings = JsonConvert.DeserializeObject<List<Screening>>(json, jsonSerializerSettings) ?? new List<Screening>();
                        PopulateScreeningMovieTitles();
                        UpdateScreeningUI(filteredScreenings);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Kivétel a dátumszűrés során: {ex.Message}", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ClearDateFilterButton_Click(object sender, RoutedEventArgs e)
        {
            ScreeningDatePicker.SelectedDate = null;
            ClearDateFilterButton.Visibility = Visibility.Collapsed;
            UpdateScreeningUI();
        }

        // JAVÍTOTT ESEMÉNYKEZELŐ
        private async void ScreeningDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is Screening screening)
            {
                if (currentUser == null)
                {
                    MessageBox.Show("A foglaláshoz be kell jelentkeznie!", "Figyelmeztetés", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    // Lekérjük a legfrissebb adatokat, beleértve a beágyazott filmadatokat
                    var res = await client.GetAsync($"/api/screenings/details/{screening.Id}");
                    if (res.IsSuccessStatusCode)
                    {
                        var json = await res.Content.ReadAsStringAsync();
                        var detailedScreening = JsonConvert.DeserializeObject<Screening>(json, jsonSerializerSettings);

                        if (detailedScreening != null)
                        {
                            var bookingWindow = new BookingWindow(client, detailedScreening, authToken) { Owner = this };
                            bookingWindow.ShowDialog();
                        }
                    }
                    else
                    {
                        MessageBox.Show("A vetítés részleteinek lekérése sikertelen.", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Kivétel a részletek lekérésekor: {ex.Message}", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void AddNewScreeningButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentUser == null || !currentUser.IsAdmin)
            {
                MessageBox.Show("Nincs jogosultságod ehhez a művelethez.", "Hiba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            AddScreeningPanel.Visibility = Visibility.Visible;
            ScreeningMovieComboBox.SelectedIndex = -1;
            ScreeningRoomInput.Text = "";
            ScreeningTimeInput.Text = "";
        }

        private void CancelAddScreeningButton_Click(object sender, RoutedEventArgs e)
        {
            AddScreeningPanel.Visibility = Visibility.Collapsed;
        }

        private async void CreateScreeningButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentUser == null || !currentUser.IsAdmin)
            { MessageBox.Show("Nincs jogosultságod!", "Hiba", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (ScreeningMovieComboBox.SelectedItem == null)
            { MessageBox.Show("Válassz filmet a vetítéshez!", "Hiányzó adat", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (string.IsNullOrWhiteSpace(ScreeningRoomInput.Text))
            { MessageBox.Show("Add meg a terem nevét!", "Hiányzó adat", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (!DateTime.TryParse(ScreeningTimeInput.Text, out DateTime time))
            { MessageBox.Show("Érvénytelen dátum formátum! Használj 'ÉÉÉÉ-HH-NN ÓÓ:PP' formátumot.", "Formátum hiba", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            var selectedMovie = (Movie)ScreeningMovieComboBox.SelectedItem;
            var screeningPayload = new ScreeningPayload { MovieId = selectedMovie.Id, Room = ScreeningRoomInput.Text, Time = time, AccountId = currentUser.Id };
            var jsonPayload = JsonConvert.SerializeObject(screeningPayload, jsonSerializerSettings);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "/api/screenings/screenings") { Content = content };
                request.Headers.Add("x-auth-token", authToken); // Token hozzáadása
                var res = await client.SendAsync(request);

                if (res.IsSuccessStatusCode)
                {
                    MessageBox.Show("Vetítés sikeresen létrehozva!", "Siker", MessageBoxButton.OK, MessageBoxImage.Information);
                    CancelAddScreeningButton_Click(null, null);
                    await RefreshAllDataAsync();
                }
                else
                {
                    var errorContent = await res.Content.ReadAsStringAsync();
                    MessageBox.Show($"Vetítés létrehozása sikertelen: {res.StatusCode} - {errorContent}", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kivétel a vetítés létrehozása során: {ex.Message}", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Keresés és UI Váltás

        private void SearchInput_TextChanged(object sender, TextChangedEventArgs e) { PerformSearch(); }

        private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (SearchInput != null) SearchInput.Text = string.Empty;
        }

        private void PerformSearch()
        {
            if (SearchInput == null || MovieList == null || allMoviesCache == null) return;
            string searchTerm = SearchInput.Text.Trim().ToLower();
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                MovieList.ItemsSource = allMoviesCache;
            }
            else
            {
                var filteredMovies = allMoviesCache.Where(m =>
                    (m.Title?.ToLower().Contains(searchTerm) ?? false) ||
                    (m.Description?.ToLower().Contains(searchTerm) ?? false) ||
                    (m.Year.ToString().Contains(searchTerm))
                ).ToList();
                MovieList.ItemsSource = filteredMovies;
            }
        }

        private void ShowRegisterButton_Click(object sender, RoutedEventArgs e)
        {
            if (LoginPanel != null) LoginPanel.Visibility = Visibility.Collapsed;
            if (RegisterPanel != null) RegisterPanel.Visibility = Visibility.Visible;
        }

        private void ShowLoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (RegisterPanel != null) RegisterPanel.Visibility = Visibility.Collapsed;
            if (LoginPanel != null) LoginPanel.Visibility = Visibility.Visible;
        }

        #endregion

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(getIDTB.Text, out _))
            {
                await LoadScreeningStatsAsync(Convert.ToInt32(getIDTB.Text));
            }
            else
            {
                MessageBox.Show("hiba, csak sámot adhatsz meg");
            }


        }
    }

    #region Data Transfer Objects (DTOs) and Models

    public class User
    {
        [JsonProperty("id")]
        public int Id { get; set; }
        public string Username { get; set; }
        public string EmailAddress { get; set; }
        public bool IsAdmin { get; set; }
    }

    public class LoginRequest
    {
        public string EmailAddress { get; set; }
        public string Password { get; set; }
    }

    public class LoginResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Token { get; set; }
        public User User { get; set; }
    }

    public class RegisterRequest
    {
        public string Username { get; set; }
        public string EmailAddress { get; set; }
        public string Password { get; set; }
    }

    public class RegisterResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<string> Messages { get; set; }
        public UserInfo User { get; set; }

        public class UserInfo
        {
            public int AccountId { get; set; }
            public string Username { get; set; }
            public string EmailAddress { get; set; }
        }
    }

    public class ErrorResponse
    {
        public string Message { get; set; }
        public List<string> Messages { get; set; }
        public bool? Success { get; set; }
    }

    public class MoviePayload
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public int Year { get; set; }
        public string Img { get; set; }
        public int AccountId { get; set; }
    }

    public class MovieDeleteRequest
    {
        public int AccountId { get; set; }
    }

    public class Movie
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int Year { get; set; }
        public string Img { get; set; }
        public string AdminName { get; set; }
    }

    public class TopMovie
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int totalBookings { get; set; } 
    }

    public class Screening
    {
        public int Id { get; set; }
        public int MovieId { get; set; }
        public string Room { get; set; }
        public DateTime Time { get; set; }
        public string AdminName { get; set; }
        public string MovieTitle { get; set; }
        public Movie Movie { get; set; }
        public string DisplayInfo => $"{MovieTitle} - {Room} terem - {Time:yyyy. MM. dd. HH:mm}";
    }

    public class ScreeningPayload
    {
        public int MovieId { get; set; }
        public string Room { get; set; }
        public DateTime Time { get; set; }
        public int AccountId { get; set; }
    }

    // ÚJ MODELLEK
    public class Booking
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ScreeningId { get; set; }
        public string Seat { get; set; }
        public DateTime CreatedAt { get; set; }
        public Screening Screening { get; set; }
        public string DisplayInfo => $"{Screening?.Movie?.Title} ({Screening?.Time:yyyy.MM.dd HH:mm}) - {Seat} hely";
    }

    public class BookingPayload
    {
        public int ScreeningId { get; set; }
        public string Seat { get; set; }
    }

    public class ScreeningStats
    {
        public int screeningId { get; set; }
        public int totalSeats { get; set; }
        public int bookingCount { get; set; }
        public double percentage { get; set; }
    }

    #endregion
}