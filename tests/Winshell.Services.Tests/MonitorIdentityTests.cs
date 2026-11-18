namespace Winshell.Services.Tests;

[TestClass]
public class MonitorIdentityTests
{
    [TestMethod]
    public void A_monitor_interface_path_splits_into_the_model_manufacturer_and_instance()
    {
        var id = @"\\?\DISPLAY#AUS32A8#5&15335277&0&UID184582#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
        Assert.AreEqual("AUS32A8", Screens.ModelCode(id));
        Assert.AreEqual("AUS", Screens.Manufacturer(Screens.ModelCode(id)));
        Assert.AreEqual("5&15335277&0&UID184582", Screens.Serial(id));
    }

    [TestMethod]
    public void A_wmi_instance_name_has_the_same_model_code_as_the_interface_path()
    {
        var id = @"\\?\DISPLAY#AUS32A8#5&15335277&0&UID184582#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
        var instance = @"DISPLAY\AUS32A8\5&15335277&0&UID184582_0";

        Assert.AreEqual("AUS32A8", Screens.ModelCode(instance));
        Assert.AreEqual(Screens.ModelCode(id), Screens.ModelCode(instance));
    }

    [TestMethod]
    public void A_monitor_windows_does_not_name_has_no_model_manufacturer_or_serial()
    {
        Assert.AreEqual("", Screens.ModelCode(""));
        Assert.AreEqual("", Screens.Manufacturer(""));
        Assert.AreEqual("", Screens.Serial(""));
    }
}
