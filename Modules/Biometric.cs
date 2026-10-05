using BiometricService;
using Microsoft.AspNetCore.Mvc;
using NITGEN.SDK.NBioBSP;
using System.Text.Json.Nodes;
using static NITGEN.SDK.NBioBSP.NBioAPI.Type;

public class Biometric
{
    private readonly APIService APIServiceInstance;

    public Biometric(APIService apiService)
    {
        APIServiceInstance = apiService;
    }

    private static readonly SemaphoreSlim _deviceLock = new(1, 1);
    private static readonly TimeSpan DeviceWait = TimeSpan.FromMinutes(2);

    private IActionResult DeviceBusy(string operation)
    {
        return new ObjectResult(
            new JsonObject
            {
                ["message"] = $"Device busy on {operation}",
                ["success"] = false
            }
        )
        { StatusCode = 503 };
    }

    private static string ErrorName(uint ret)
    {
        var field = typeof(NBioAPI.Error)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .FirstOrDefault(f => f.FieldType == typeof(int)
                && unchecked((uint)(int)f.GetRawConstantValue()!) == ret);
        return field?.Name ?? ret.ToString();
    }

    private IActionResult SdkError(string operation, uint ret, string? message = null)
    {
        var name = ErrorName(ret);
        return new BadRequestObjectResult(
            new JsonObject
            {
                ["message"] = message ?? $"Error on {operation}: {name}",
                ["errorCode"] = ret,
                ["errorName"] = name,
                ["success"] = false
            }
        );
    }

    private IActionResult CaptureError(string operation, Exception ex)
    {
        try { APIServiceInstance._NBioAPI.CloseDevice(NBioAPI.Type.DEVICE_ID.AUTO); }
        catch { }
        return new ObjectResult(
            new JsonObject
            {
                ["message"] = $"Error on {operation}: {ex.Message}",
                ["success"] = false
            }
        )
        { StatusCode = 500 };
    }

    public IActionResult CaptureHash(bool img = false)
    {
        if (!_deviceLock.Wait(DeviceWait)) return DeviceBusy(nameof(CaptureHash));
        try
        {
            HFIR auditHFIR = new HFIR();
            uint ret;
            NBioAPI.Type.HFIR hCapturedFIR;
            int quality;
            try
            {
                APIServiceInstance._NBioAPI.OpenDevice(NBioAPI.Type.DEVICE_ID.AUTO);
                ret = APIServiceInstance._NBioAPI.Capture(NBioAPI.Type.FIR_PURPOSE.ENROLL, out hCapturedFIR, NBioAPI.Type.TIMEOUT.DEFAULT, auditHFIR, null);

                APIServiceInstance._NBioAPI.GetFIRFromHandle(auditHFIR, out NBioAPI.Type.FIR auditFIR);
                quality = auditFIR.Header.Quality;
            }
            finally
            {
                APIServiceInstance._NBioAPI.CloseDevice(NBioAPI.Type.DEVICE_ID.AUTO);
            }
            if (ret != NBioAPI.Error.NONE) return SdkError("Capture", ret);

            NBioAPI.Export NBioExport = new NBioAPI.Export(APIServiceInstance._NBioAPI);
            NBioExport.NBioBSPToImage(auditHFIR, out NBioAPI.Export.EXPORT_AUDIT_DATA exportAuditData);

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

            string[] images = new string[10];
            List<byte> fingers = new List<byte> { };

            foreach (NBioAPI.Export.AUDIT_DATA finger in exportAuditData.AuditData)
            {
                APIServiceInstance._NBioAPI.ImgConvRawToJpgBuf(finger.Image[0].Data, exportAuditData.ImageWidth, exportAuditData.ImageHeight, 1, out byte[] imgData);
                Directory.CreateDirectory(tempPath);
                File.WriteAllBytes($"{tempPath}\\finger_{finger.FingerID}.jpg", imgData);
                images[finger.FingerID - 1] = Convert.ToBase64String(imgData);
                fingers.Add(finger.FingerID);
            }

            if (!img)
            {
                return new OkObjectResult(
                    new JsonObject
                    {
                        ["fingers-registered"] = exportAuditData.AuditData.GetLength(0),
                        ["template"] = textFIR.TextFIR,
                        ["fingers-id"] = new JsonArray(fingers.Select(finger => JsonValue.Create(finger)).ToArray()),
                        ["quality-FIR"] = quality,
                        ["success"] = true,
                    }
                );
            }
            else
            {
                return new OkObjectResult(
                    new JsonObject
                    {
                        ["fingers-registered"] = exportAuditData.AuditData.GetLength(0),
                        ["template"] = textFIR.TextFIR,
                        ["fingers-id"] = new JsonArray(fingers.Select(finger => JsonValue.Create(finger)).ToArray()),
                        ["images"] = new JsonArray(images.Select(image => JsonValue.Create(image)).ToArray()),
                        ["quality-FIR"] = quality,
                        ["success"] = true,
                    }
                );
            }
        }
        catch (Exception ex)
        {
            return CaptureError("Capture", ex);
        }
        finally
        {
            _deviceLock.Release();
        }
    }

    public IActionResult CaptureForVerify(uint windowVisibility = NBioAPI.Type.WINDOW_STYLE.POPUP)
    {
        if (!_deviceLock.Wait(DeviceWait)) return DeviceBusy(nameof(CaptureForVerify));
        try
        {
            HFIR auditHFIR = new HFIR();

            NBioAPI.Type.WINDOW_OPTION windowOption = new NBioAPI.Type.WINDOW_OPTION();
            windowOption.WindowStyle = windowVisibility;

            uint ret;
            NBioAPI.Type.HFIR hCapturedFIR;
            try
            {
                APIServiceInstance._NBioAPI.OpenDevice(NBioAPI.Type.DEVICE_ID.AUTO);
                ret = APIServiceInstance._NBioAPI.Capture(NBioAPI.Type.FIR_PURPOSE.VERIFY, out hCapturedFIR, NBioAPI.Type.TIMEOUT.DEFAULT, auditHFIR, windowOption);
            }
            finally
            {
                APIServiceInstance._NBioAPI.CloseDevice(NBioAPI.Type.DEVICE_ID.AUTO);
            }
            if (ret != NBioAPI.Error.NONE) return SdkError("Capture", ret);

            APIServiceInstance._NBioAPI.GetTextFIRFromHandle(hCapturedFIR, out NBioAPI.Type.FIR_TEXTENCODE textFIR, true);
            NBioAPI.Export NBioExport = new NBioAPI.Export(APIServiceInstance._NBioAPI);
            NBioExport.NBioBSPToImage(auditHFIR, out NBioAPI.Export.EXPORT_AUDIT_DATA exportAuditData);
            APIServiceInstance._NBioAPI.ImgConvRawToJpgBuf(exportAuditData.AuditData[0].Image[0].Data, exportAuditData.ImageWidth, exportAuditData.ImageHeight, 1, out byte[] imgData);
            string image64 = Convert.ToBase64String(imgData);

            return new OkObjectResult(
                new JsonObject
                {
                    ["template"] = textFIR.TextFIR,
                    ["image"] = image64,
                    ["success"] = true
                }
            );
        }
        catch (Exception ex)
        {
            return CaptureError("Capture", ex);
        }
        finally
        {
            _deviceLock.Release();
        }
    }

    public IActionResult IdentifyOneOnOne(JsonObject template, bool img = false, uint windowVisibility = NBioAPI.Type.WINDOW_STYLE.POPUP)
    {
        if (!_deviceLock.Wait(DeviceWait)) return DeviceBusy(nameof(IdentifyOneOnOne));
        try
        {
            var secondFir = new NBioAPI.Type.FIR_TEXTENCODE { TextFIR = template["template"]?.ToString() };
            HFIR auditHFIR = new HFIR();

            NBioAPI.Type.WINDOW_OPTION windowOption = new NBioAPI.Type.WINDOW_OPTION();
            windowOption.WindowStyle = windowVisibility;

            uint ret;
            bool matched;
            try
            {
                APIServiceInstance._NBioAPI.OpenDevice(NBioAPI.Type.DEVICE_ID.AUTO);
                ret = APIServiceInstance._NBioAPI.Verify(secondFir, out matched, null, -1, auditHFIR, windowOption);
            }
            finally
            {
                APIServiceInstance._NBioAPI.CloseDevice(NBioAPI.Type.DEVICE_ID.AUTO);
            }
            if (ret != NBioAPI.Error.NONE) return new BadRequestObjectResult(
                new JsonObject
                {
                    ["message"] = ret == NBioAPI.Error.CAPTURE_TIMEOUT ? "Timeout" : $"Error on Verify: {ErrorName(ret)}",
                    ["errorCode"] = ret,
                    ["errorName"] = ErrorName(ret),
                    ["success"] = false
                }
            );

            if (!img)
            {
                return new OkObjectResult(
                    new JsonObject
                    {
                        ["message"] = matched ? "Fingerprint matches" : "Fingerprint doesnt match",
                        ["success"] = matched
                    }
                );
            }
            else
            {
                NBioAPI.Export NBioExport = new NBioAPI.Export(APIServiceInstance._NBioAPI);
                NBioExport.NBioBSPToImage(auditHFIR, out NBioAPI.Export.EXPORT_AUDIT_DATA exportAuditData);
                APIServiceInstance._NBioAPI.ImgConvRawToJpgBuf(exportAuditData.AuditData[0].Image[0].Data, exportAuditData.ImageWidth, exportAuditData.ImageHeight, 1, out byte[] imgData);
                string image64 = Convert.ToBase64String(imgData);

                return new OkObjectResult(
                    new JsonObject
                    {
                        ["message"] = matched ? "Fingerprint matches" : "Fingerprint doesnt match",
                        ["image"] = image64,
                        ["success"] = matched
                    }
                );
            }
        }
        catch (Exception ex)
        {
            return CaptureError("Verify", ex);
        }
        finally
        {
            _deviceLock.Release();
        }
    }

    public IActionResult Identification(uint secuLevel = NBioAPI.Type.FIR_SECURITY_LEVEL.NORMAL, bool img = false, uint windowVisibility = NBioAPI.Type.WINDOW_STYLE.POPUP)
    {
        if (!_deviceLock.Wait(DeviceWait)) return DeviceBusy(nameof(Identification));
        try
        {
            HFIR auditHFIR = new HFIR();

            NBioAPI.Type.WINDOW_OPTION windowOption = new NBioAPI.Type.WINDOW_OPTION();
            windowOption.WindowStyle = windowVisibility;

            uint ret;
            NBioAPI.Type.HFIR hCapturedFIR;
            try
            {
                APIServiceInstance._NBioAPI.OpenDevice(NBioAPI.Type.DEVICE_ID.AUTO);
                ret = APIServiceInstance._NBioAPI.Capture(NBioAPI.Type.FIR_PURPOSE.VERIFY, out hCapturedFIR, NBioAPI.Type.TIMEOUT.DEFAULT, auditHFIR, windowOption);
            }
            finally
            {
                APIServiceInstance._NBioAPI.CloseDevice(NBioAPI.Type.DEVICE_ID.AUTO);
            }
            if (ret != NBioAPI.Error.NONE) return SdkError("Capture", ret);

            NBioAPI.IndexSearch.CALLBACK_INFO_0 cbInfo = new();
            APIServiceInstance._IndexSearch.IdentifyData(hCapturedFIR, secuLevel, out NBioAPI.IndexSearch.FP_INFO fpInfo, cbInfo);

            if (!img)
            {
                return new OkObjectResult(
                    new JsonObject
                    {
                        ["message"] = fpInfo.ID != 0 ? "Fingerprint match found" : "Fingerprint match not found",
                        ["id"] = fpInfo.ID,
                        ["success"] = fpInfo.ID != 0
                    }
                );
            }
            else
            {
                NBioAPI.Export NBioExport = new NBioAPI.Export(APIServiceInstance._NBioAPI);
                NBioExport.NBioBSPToImage(auditHFIR, out NBioAPI.Export.EXPORT_AUDIT_DATA exportAuditData);
                APIServiceInstance._NBioAPI.ImgConvRawToJpgBuf(exportAuditData.AuditData[0].Image[0].Data, exportAuditData.ImageWidth, exportAuditData.ImageHeight, 1, out byte[] imgData);
                string image64 = Convert.ToBase64String(imgData);

                return new OkObjectResult(
                    new JsonObject
                    {
                        ["message"] = fpInfo.ID != 0 ? "Fingerprint match found" : "Fingerprint match not found",
                        ["id"] = fpInfo.ID,
                        ["image"] = image64,
                        ["success"] = fpInfo.ID != 0
                    }
                );
            }
        }
        catch (Exception ex)
        {
            return CaptureError("Capture", ex);
        }
        finally
        {
            _deviceLock.Release();
        }
    }

    public IActionResult LoadToMemory(JsonArray fingers)
    {
        if (!_deviceLock.Wait(DeviceWait)) return DeviceBusy(nameof(LoadToMemory));
        try
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
                if (ret != NBioAPI.Error.NONE) return SdkError("AddFIR", ret);
            }

            return new OkObjectResult(
                new JsonObject
                {
                    ["message"] = "Templates loaded to memory",
                    ["success"] = true
                }
            );
        }
        catch (Exception ex)
        {
            return CaptureError("AddFIR", ex);
        }
        finally
        {
            _deviceLock.Release();
        }
    }

    public IActionResult DeleteAllFromMemory()
    {
        if (!_deviceLock.Wait(DeviceWait)) return DeviceBusy(nameof(DeleteAllFromMemory));
        try
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
        catch (Exception ex)
        {
            return CaptureError("ClearDB", ex);
        }
        finally
        {
            _deviceLock.Release();
        }
    }

    public IActionResult TotalIdsInMemory()
    {
        if (!_deviceLock.Wait(DeviceWait)) return DeviceBusy(nameof(TotalIdsInMemory));
        try
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
        catch (Exception ex)
        {
            return CaptureError("GetDataCount", ex);
        }
        finally
        {
            _deviceLock.Release();
        }
    }

    public IActionResult DeviceUniqueSerialID()
    {
        if (!_deviceLock.Wait(DeviceWait)) return DeviceBusy(nameof(DeviceUniqueSerialID));
        try
        {
            byte[] deviceId;
            try
            {
                APIServiceInstance._NBioAPI.OpenDevice(NBioAPI.Type.DEVICE_ID.AUTO);
                byte[] input = new byte[8];
                APIServiceInstance._NBioAPI.DeviceIoControl(514, input, out deviceId);
            }
            finally
            {
                APIServiceInstance._NBioAPI.CloseDevice(NBioAPI.Type.DEVICE_ID.AUTO);
            }
            return new OkObjectResult(
                new JsonObject
                {
                    ["serial"] = BitConverter.ToString(deviceId),
                    ["success"] = true
                }
            );
        }
        catch (Exception ex)
        {
            return CaptureError("DeviceIoControl", ex);
        }
        finally
        {
            _deviceLock.Release();
        }
    }

    public IActionResult JoinTemplates(JsonArray fingers)
    {
        if (!_deviceLock.Wait(DeviceWait)) return DeviceBusy(nameof(JoinTemplates));
        try
        {
            if (fingers.Count < 2) return new BadRequestObjectResult(
                                       new JsonObject
                                       {
                                           ["message"] = "No templates to join",
                                           ["success"] = false
                                       });

            List<string> list = [];
            list.AddRange(fingers.Select(fingerObject => fingerObject["template"].ToString()));

            NBioAPI.Type.FIR_PAYLOAD payload = new NBioAPI.Type.FIR_PAYLOAD();
            for (int i = 1; i < fingers.Count; i++)
            {
                NBioAPI.Type.FIR_TEXTENCODE textFIR1 = new NBioAPI.Type.FIR_TEXTENCODE() { TextFIR = list[i - 1] };
                NBioAPI.Type.FIR_TEXTENCODE textFIR2 = new NBioAPI.Type.FIR_TEXTENCODE() { TextFIR = list[i] };
                APIServiceInstance._NBioAPI.CreateTemplate(textFIR1, textFIR2, out NBioAPI.Type.HFIR hNew, payload);
                uint ret = APIServiceInstance._NBioAPI.GetTextFIRFromHandle(hNew, out NBioAPI.Type.FIR_TEXTENCODE newTextFIR, true);
                if (ret != NBioAPI.Error.NONE) return SdkError("CreateTemplate", ret);
                list[i] = newTextFIR.TextFIR;
            }
            return new OkObjectResult(
                new JsonObject
                {
                    ["template"] = list[fingers.Count - 1],
                    ["message"] = "Templates joined successfully",
                    ["success"] = true
                });
        }
        catch (Exception ex)
        {
            return CaptureError("CreateTemplate", ex);
        }
        finally
        {
            _deviceLock.Release();
        }
    }
}
