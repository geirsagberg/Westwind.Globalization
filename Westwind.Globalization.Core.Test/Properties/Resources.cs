using Westwind.Globalization.Core.DbResourceManager;
using Westwind.Globalization.Core.Utilities;

namespace Westwind.Globalization.Test
{
    public class GeneratedResourceSettings
    {
        // You can change the ResourceAccess Mode globally in Application_Start        
	public static ResourceAccessMode ResourceAccessMode = ResourceAccessMode.DbResourceManager;
    }

    public class CommonWords
    {
	public static string Save => DbRes.T("Save", "CommonWords");

	public static string Cancel => DbRes.T("Cancel", "CommonWords");

	public static string Edit => DbRes.T("Edit", "CommonWords");
    }

    public class Resources
    {
	public static string NameIsRequired => DbRes.T("NameIsRequired", "Resources");

	public static string Today => DbRes.T("Today", "Resources");

	public static string Yesterday => DbRes.T("Yesterday", "Resources");

	public static string HelloWorld => DbRes.T("HelloWorld", "Resources");

	public static string Ready => DbRes.T("Ready", "Resources");

	public static string AddressIsRequired => DbRes.T("AddressIsRequired", "Resources");
    }
}