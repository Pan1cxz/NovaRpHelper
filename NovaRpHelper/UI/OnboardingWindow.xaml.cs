using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using NovaRpHelper.Core;

namespace NovaRpHelper.UI
{
    public partial class OnboardingWindow : Window
    {
        /// <summary>
        /// Ранги/должности из официальных уставов NOVA RP.
        /// </summary>
        private static readonly Dictionary<string, string[]> GovRanks = new()
        {
            ["Правительство"] = new[]
            {
                "Губернатор",
                "Вице-Губернатор",
                "Министр Национальной Безопасности",
                "Министр Финансов",
                "Министр Культуры",
                "Министр Юстиции",
                "Министр Здравоохранения",
                "Глава Аппарата Правительства",
                "Генеральный Прокурор",
                "Верховный Судья"
            },
            ["Судебная власть"] = new[]
            {
                "Верховный суд",
                "Окружной суд",
                "Апелляционный суд",
                "Судебный архив"
            },
            ["Центральная полиция"] = new[]
            {
                "Шеф",
                "Зам. Шефа",
                "Ассистент Шефа",
                "Глава Отдела",
                "Зам. Главы Отдела",
                "Инспектор",
                "Капитан",
                "Лейтенант II",
                "Лейтенант I",
                "Сержант II",
                "Сержант I",
                "Офицер II",
                "Офицер I",
                "Кадет"
            },
            ["Дорожная полиция"] = new[]
            {
                "Sheriff",
                "UnderSheriff",
                "Curator of Department",
                "Head of Department",
                "Deputy of Department",
                "Instructor of Department",
                "Lieutenant",
                "Jr. Lieutenant",
                "St. Sergeant",
                "Sergeant",
                "Jr. Sergeant",
                "Officer",
                "Jr. Officer",
                "Cadet",
                "Jr.Cadet"
            },
            ["Национальная гвардия"] = new[]
            {
                "Рекрут",
                "Рядовой",
                "Специалист",
                "Капрал",
                "Сержант",
                "Штаб-Сержант",
                "Мастер-Сержант",
                "Сержант-Майор",
                "Уорэнт-Офицер",
                "Старший Уорэнт-Офицер",
                "Лейтенант",
                "Капитан",
                "Майор",
                "Подполковник",
                "Полковник",
                "Генерал-Майор",
                "Генерал-Лейтенант",
                "Генерал Армии"
            },
            ["EMS"] = new[]
            {
                "Главный Врач (16)",
                "Заместитель Главного Врача (15)",
                "Заведующий Отделением (14)",
                "Заместитель Заведующего Отделением (13)",
                "Инструктор (12)",
                "Инспектор (11)",
                "Клинический специалист (10)",
                "Главный специалист (9)",
                "Ведущий специалист (8)",
                "Старший фельдшер (7)",
                "Фельдшер (6)",
                "Терапевт (5)",
                "Дежурный медик (4)",
                "Ординатор (3)",
                "Интерн (2)",
                "Студент-медик (1)"
            },
            ["Федеральное Расследовательское Бюро"] = new[]
            {
                "Director",
                "Deputy Director",
                "Head of Department",
                "Deputy Head of Department",
                "Instructor",
                "Lead Agent",
                "Special Agent",
                "Junior Special Agent",
                "Senior Agent",
                "Security Agent",
                "Agent",
                "Junior Agent",
                "Office Agent",
                "Trainee Agent"
            }
        };

        public OnboardingWindow()
        {
            InitializeComponent();
        }

        private void Role_Changed(object sender, RoutedEventArgs e)
        {
            GovPanel.Visibility = RoleGov.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            CrimePanel.Visibility = RoleCrime.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            ErrorText.Text = string.Empty;
        }

        private void Faction_Changed(object sender, SelectionChangedEventArgs e)
        {
            GovRankCombo.Items.Clear();
            string faction = (FactionBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;

            if (GovRanks.TryGetValue(faction, out var ranks))
            {
                GovRankComboLabel.Text = faction == "Судебная власть" ? "Инстанция:" : "Звание / должность:";
                foreach (var rank in ranks)
                    GovRankCombo.Items.Add(rank);
                GovRankComboPanel.Visibility = Visibility.Visible;
                GovRankTextPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                GovRankComboPanel.Visibility = Visibility.Collapsed;
                GovRankTextPanel.Visibility = Visibility.Visible;
            }
        }

        private string SelectedRole()
        {
            if (RoleAdmin.IsChecked == true) return "Admin";
            if (RoleGov.IsChecked == true) return "Gov";
            if (RoleCrime.IsChecked == true) return "Crime";
            if (RoleCivil.IsChecked == true) return "Civilian";
            return string.Empty;
        }

        private static string ComboValue(ComboBox box)
        {
            return (box.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            string nick = NickBox.Text.Trim();
            string role = SelectedRole();

            if (string.IsNullOrWhiteSpace(nick) || !nick.Contains("_") || nick.StartsWith("_") || nick.EndsWith("_"))
            {
                ErrorText.Text = "Ник должен быть в формате Name_Surname (например, Ivan_Petrov).";
                return;
            }

            if (string.IsNullOrEmpty(role))
            {
                ErrorText.Text = "Выбери, кем ты являешься на сервере.";
                return;
            }

            string govFaction = string.Empty;
            string govRank = string.Empty;
            string crimeOrg = string.Empty;
            string crimeRank = string.Empty;

            if (role == "Gov")
            {
                govFaction = ComboValue(FactionBox);
                if (string.IsNullOrWhiteSpace(govFaction))
                {
                    ErrorText.Text = "Выбери фракцию.";
                    return;
                }

                govRank = GovRankComboPanel.Visibility == Visibility.Visible
                    ? ComboValue(GovRankCombo)
                    : GovRankText.Text.Trim();

                if (string.IsNullOrWhiteSpace(govRank))
                {
                    ErrorText.Text = "Укажи звание или должность.";
                    return;
                }
            }
            else if (role == "Crime")
            {
                crimeOrg = ComboValue(CrimeOrgBox);
                crimeRank = CrimeRankBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(crimeOrg) || string.IsNullOrWhiteSpace(crimeRank))
                {
                    ErrorText.Text = "Укажи организацию и ранг.";
                    return;
                }
            }

            try
            {
                await ConfigService.Instance.UpdateSettingsAsync(s =>
                {
                    s.Profile.Nick = nick;
                    s.Profile.Role = role;
                    s.Profile.AdminRank = string.Empty; // ранги администрации не используются
                    s.Profile.GovFaction = govFaction;
                    s.Profile.GovRank = govRank;
                    s.Profile.CrimeOrg = crimeOrg;
                    s.Profile.CrimeRank = crimeRank;
                });
            }
            catch (Exception ex)
            {
                ErrorText.Text = $"Не удалось сохранить профиль: {ex.Message}";
                return;
            }

            DialogResult = true;
            Close();
        }
    }
}