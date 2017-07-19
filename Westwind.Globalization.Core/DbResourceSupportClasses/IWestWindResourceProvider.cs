namespace Westwind.Globalization.Core.DbResourceSupportClasses
{
    /// <summary>
    /// Resource Provider marker interface. Also provides for clearing resources.
    /// </summary>
    public interface IWestWindResourceProvider
    {
        /// <summary>
        /// Interface method used to force providers to register themselves
        /// with DbResourceConfiguration.
        /// </summary>
        void ClearResourceCache();
    }
}