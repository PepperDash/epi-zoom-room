using System.ComponentModel;
using PepperDash.Core;

namespace PDT.Plugins.Zoom.Room
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
                Debug.Console(2, "PropertyChanged event is NULL");
            }
		}

		#endregion
	}
}