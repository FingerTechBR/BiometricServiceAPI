using BiometricService;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NITGEN.SDK.NBioBSP;
using System.Text.Json.Nodes;
using static NITGEN.SDK.NBioBSP.NBioAPI.Export;
using static NITGEN.SDK.NBioBSP.NBioAPI.Type;

public class Biometric
{
    private readonly APIService APIServiceInstance;

    public Biometric(APIService apiService)
    {
        APIServiceInstance = apiService;
    }
    public IActionResult CaptureHash()
    {
        HFIR auditHFIR = new();
        APIServiceInstance._NBioAPI.OpenDevice(NBioAPI.Type.DEVICE_ID.AUTO);
        uint ret = APIServiceInstance._NBioAPI.Capture(NBioAPI.Type.FIR_PURPOSE.ENROLL, out NBioAPI.Type.HFIR hCapturedFIR, NBioAPI.Type.TIMEOUT.DEFAULT, auditHFIR, null);
        APIServiceInstance._NBioAPI.CloseDevice(NBioAPI.Type.DEVICE_ID.AUTO);
        if (ret != NBioAPI.Error.NONE) return new BadRequestObjectResult(
            new JsonObject
            {
                ["message"] = $"Error on Capture: {ret}",
                ["success"] = false
            }
        );

        NBioAPI.Export NBioExport = new(APIServiceInstance._NBioAPI);
        NBioExport.NBioBSPToImage(auditHFIR, out EXPORT_AUDIT_DATA exportAuditData);

        string tempPath = Environment.ExpandEnvironmentVariables(@"%TEMP%\fingers-registered");

        if (!Directory.Exists(tempPath))
        {
            Directory.CreateDirectory(tempPath);
        }

        DirectoryInfo directoryInfo = new DirectoryInfo(tempPath);
        FileInfo[] files = directoryInfo.GetFiles("*.*", SearchOption.TopDirectoryOnly);
        foreach (FileInfo file in files)
        {
            if (file.Extension.ToLower() == ".jpg")
            {
                file.Delete();
            }
        }

        APIServiceInstance._NBioAPI.GetTextFIRFromHandle(hCapturedFIR, out NBioAPI.Type.FIR_TEXTENCODE textFIR, true);

        foreach (AUDIT_DATA finger in exportAuditData.AuditData)
        {
            APIServiceInstance._NBioAPI.ImgConvRawToJpgBuf(finger.Image[0].Data, exportAuditData.ImageWidth, exportAuditData.ImageHeight, 1, out byte[] imgData);
            Directory.CreateDirectory(tempPath);
            File.WriteAllBytes($"{tempPath}\\finger_{finger.FingerID}.jpg", imgData);
        }

        return new OkObjectResult(
        new JsonObject
        {
            ["fingers-registered"] = exportAuditData.AuditData.GetLength(0),
            ["template"] = textFIR.TextFIR,
            ["success"] = true,
        });
    }

    public IActionResult IdentifyOneOnOne(JsonObject template)
    {
        var secondFir = new NBioAPI.Type.FIR_TEXTENCODE { TextFIR = template["template"]?.ToString() };

        APIServiceInstance._NBioAPI.OpenDevice(NBioAPI.Type.DEVICE_ID.AUTO);
        uint ret = APIServiceInstance._NBioAPI.Verify(secondFir, out bool matched, null);
        APIServiceInstance._NBioAPI.CloseDevice(NBioAPI.Type.DEVICE_ID.AUTO);
        if (ret != NBioAPI.Error.NONE) return new BadRequestObjectResult(
            new JsonObject
            {
                ["message"] = ret == NBioAPI.Error.CAPTURE_TIMEOUT ? "Timeout" : $"Error on Verify: {ret}",
                ["success"] = false
            }
        );

        return new OkObjectResult(
            new JsonObject
            {
                ["message"] = matched ? "Fingerprint matches" : "Fingerprint doesnt match",
                ["success"] = matched
            }
        );
    }

    public IActionResult Identification()
    {
        APIServiceInstance._NBioAPI.OpenDevice(NBioAPI.Type.DEVICE_ID.AUTO);
        uint ret = APIServiceInstance._NBioAPI.Capture(NBioAPI.Type.FIR_PURPOSE.VERIFY, out NBioAPI.Type.HFIR hCapturedFIR, NBioAPI.Type.TIMEOUT.DEFAULT, null, null);
        APIServiceInstance._NBioAPI.CloseDevice(NBioAPI.Type.DEVICE_ID.AUTO);
        if (ret != NBioAPI.Error.NONE) return new BadRequestObjectResult(
            new JsonObject
            {
                ["message"] = $"Error on Capture: {ret}",
                ["success"] = false
            }
        );

        NBioAPI.IndexSearch.CALLBACK_INFO_0 cbInfo = new();
        APIServiceInstance._IndexSearch.IdentifyData(hCapturedFIR, NBioAPI.Type.FIR_SECURITY_LEVEL.NORMAL, out NBioAPI.IndexSearch.FP_INFO fpInfo, cbInfo);

        return new OkObjectResult(
            new JsonObject
            {
                ["message"] = fpInfo.ID != 0 ? "Fingerprint match found" : "Fingerprint match not found",
                ["id"] = fpInfo.ID,
                ["success"] = fpInfo.ID != 0
            }
        );

    }

    public IActionResult LoadToMemory(JsonArray fingers)
    {
        if (fingers.Count == 0)
        {
            return new BadRequestObjectResult(
                new JsonObject
                {
                    ["message"] = "No templates to load",
                    ["success"] = false
                }
            );
        }

        uint ret;
        var textFir = new NBioAPI.Type.FIR_TEXTENCODE();
        foreach (JsonObject fingerObject in fingers)
        {
            textFir.TextFIR = fingerObject["template"].ToString();
            ret = APIServiceInstance._IndexSearch.AddFIR(textFir, (uint)fingerObject["id"], out _);
            if (ret != NBioAPI.Error.NONE) return new BadRequestObjectResult(
                new JsonObject
                {
                    ["message"] = $"Error on AddFIR: {ret}",
                    ["success"] = false
                }
            );
        }

        return new OkObjectResult(
            new JsonObject
            {
                ["message"] = "Templates loaded to memory",
                ["success"] = true
            }
        );
    }

    public IActionResult DeleteAllFromMemory()
    {
        APIServiceInstance._IndexSearch.ClearDB();
        return new OkObjectResult(
            new JsonObject
            {
                ["message"] = "All templates deleted from memory",
                ["success"] = true
            }
        );
    }

    public IActionResult TotalIdsInMemory()
    {
        APIServiceInstance._IndexSearch.GetDataCount(out UInt32 dataCount);
        return new OkObjectResult(
            new JsonObject
            {
                ["total"] = dataCount,
                ["success"] = true
            }
        );
    }

    public IActionResult DeviceUniqueSerialID()
    {
        APIServiceInstance._NBioAPI.OpenDevice(NBioAPI.Type.DEVICE_ID.AUTO);
        byte[] input = new byte[8];
        APIServiceInstance._NBioAPI.DeviceIoControl(514, input, out byte[] deviceId);
        APIServiceInstance._NBioAPI.CloseDevice(NBioAPI.Type.DEVICE_ID.AUTO);
        return new OkObjectResult(
            new JsonObject
            {
                ["serial"] = BitConverter.ToString(deviceId),
                ["success"] = true
            }
        );
    }

    public IActionResult Snapshot()
    {
        APIServiceInstance._NBioAPI.OpenDevice(NBioAPI.Type.DEVICE_ID.AUTO);
        uint ret = APIServiceInstance._NBioAPI.Capture(NBioAPI.Type.FIR_PURPOSE.VERIFY, out NBioAPI.Type.HFIR hCapturedFIR, NBioAPI.Type.TIMEOUT.DEFAULT, null, null);
        APIServiceInstance._NBioAPI.CloseDevice(NBioAPI.Type.DEVICE_ID.AUTO);
        if (ret != NBioAPI.Error.NONE) return new BadRequestObjectResult(
            new JsonObject
            {
                ["message"] = $"Error on Capture: {ret}",
                ["success"] = false
            }
        );

        APIServiceInstance._NBioAPI.GetTextFIRFromHandle(hCapturedFIR, out NBioAPI.Type.FIR_TEXTENCODE textFIR, true);

        return new OkObjectResult(
            new JsonObject
            {
                ["template"] = textFIR.TextFIR,
                ["success"] = true
            }
        );
    }

}