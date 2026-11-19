namespace Winshell.Tests;

[TestClass]
public class IpcShowJsonTests
{
    [TestMethod]
    public void Ipc_show_json_prints_each_target_indented_with_camel_case_names()
    {
        var payload = IpcShowPayload.Write(
        [
            new IpcTargetView(
                "café",
                [new IpcFunctionView("note", [new IpcParameterView("text", "string"), new IpcParameterView("any", null)], "void")],
                [new IpcPropertyView("value", "int")],
                [new IpcSignalView("changed", [])])
        ]);

        var expected = """
                       [
                         {
                           "target": "café",
                           "functions": [
                             {
                               "name": "note",
                               "parameters": [
                                 {
                                   "name": "text",
                                   "type": "string"
                                 },
                                 {
                                   "name": "any",
                                   "type": null
                                 }
                               ],
                               "returns": "void"
                             }
                           ],
                           "properties": [
                             {
                               "name": "value",
                               "type": "int"
                             }
                           ],
                           "signals": [
                             {
                               "name": "changed",
                               "parameters": []
                             }
                           ]
                         }
                       ]
                       """.ReplaceLineEndings("\r\n");
        Assert.AreEqual(expected, CliJson.IpcShow(payload));
    }
}
