using System.Collections.Generic;
using System.Globalization;
using Westwind.Globalization.Core.DbResourceDataManager;

namespace Westwind.Globalization.Core.Web.Administration
{
    /// <summary>
    /// Class that holds a resource item with all of its detail
    /// information.
    /// </summary>
    public class ResourceItemEx : ResourceItem
    {
        public ResourceItemEx()
        {
        }

        public ResourceItemEx(ResourceItem item)
        {
            ResourceId = item.ResourceId;
            LocaleId = item.LocaleId;
            Value = item.Value;
            ResourceSet = item.ResourceSet;
            Type = item.Type;
            FileName = item.FileName;
            TextFile = item.TextFile;
            BinFile = item.BinFile;
            Comment = item.Comment;
            ValueType = item.ValueType;
        }

        public bool IsRtl
        {
            get
            {
                if (_isRtl != null)
                    return _isRtl.Value;

                _isRtl = false;
                try
                {
                    var li = LocaleId;
                    if (string.IsNullOrEmpty(LocaleId))
                        li = CultureInfo.InstalledUICulture.IetfLanguageTag;

                    var ci = CultureInfo.GetCultureInfoByIetfLanguageTag(LocaleId);
                    _isRtl = ci.TextInfo.IsRightToLeft;

                    return _isRtl.Value;
                }
                catch { }

                return _isRtl.Value;
            }
            set
            {
                _isRtl = value;
            }
        }
        private bool? _isRtl;

        public List<ResourceString> ResourceList { get; set; }

    }
}