using System.ComponentModel;
using PepperDash.Core;
using Serilog.Events;

namespace PepperDash.Essentials.Plugins.Zoom.ZoomRoom
{
    public abstract class NotifiableObject : INotifyPropertyChanged
	{
		#region INotifyPropertyChanged Members

		public event PropertyChangedEventHandler PropertyChanged;

		protected void NotifyPropertyChanged(string propertyName)
		{
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
            else
            {
                Debug.LogMessage(LogEventLevel.Debug, "PropertyChanged event is NULL");
            }
		}

		#endregion
	}
}