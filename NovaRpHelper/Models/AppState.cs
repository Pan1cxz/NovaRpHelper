using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NovaRpHelper.Models
{
    /// <summary>
    /// Глобальное состояние приложения, доступное для привязки в UI (INotifyPropertyChanged)
    /// и для чтения из модулей (например, для подстановки {ID}).
    /// </summary>
    public class AppState : INotifyPropertyChanged
    {
        private static readonly Lazy<AppState> _instance = new(() => new AppState());
        public static AppState Instance => _instance.Value;

        private string _targetId = string.Empty;

        /// <summary>
        /// Текущий Target ID, введенный пользователем в оверлее.
        /// Используется для замены тега {ID} в биндах.
        /// </summary>
        public string TargetId
        {
            get => _targetId;
            set
            {
                if (_targetId != value)
                {
                    _targetId = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}