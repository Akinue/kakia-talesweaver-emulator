using Yggdrasil.Events;
using Yggdrasil.Scripting;

namespace Kakia.TW.World.Scripting
{
	/// <summary>
	/// General purpose script class.
	/// </summary>
	public abstract class GeneralScript : IScript, IDisposable
	{
		/// <summary>
		/// Initializes script.
		/// </summary>
		/// <returns></returns>
		public virtual bool Init()
		{
			Load();

			OnAttribute.Load(this, WorldServer.Instance.ServerEvents);

			return true;
		}

		/// <summary>
		/// Called when the script is being removed before a reload.
		/// </summary>
		public virtual void Dispose()
		{
			OnAttribute.Unload(this, WorldServer.Instance.ServerEvents);
		}

		/// <summary>
		/// Called when the script is being initialized.
		/// </summary>
		public virtual void Load()
		{
		}
	}
}
