package com.jawtracking.fileaccess;

import android.app.Activity;
import android.content.Intent;
import android.database.Cursor;
import android.net.Uri;
import android.os.Bundle;
import android.provider.OpenableColumns;

import org.json.JSONObject;

import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.lang.reflect.Field;
import java.lang.reflect.Method;
import java.util.Locale;

public final class JawFilePickerActivity extends Activity {
    private static final int RequestCode = 61927;
    private static final String CallbackObjectName = "JawFilePickerBridge";
    private static final String CallbackMethodName = "OnAndroidFilePicked";

    private boolean pickerStarted;

    public static void openPicker(String title) {
        Activity activity = getUnityActivity();
        if (activity == null) {
            sendFailure("Unity Android aktivitesi bulunamadı.");
            return;
        }

        Intent intent = new Intent(activity, JawFilePickerActivity.class);
        intent.putExtra("title", title);
        activity.startActivity(intent);
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        if (savedInstanceState != null) {
            pickerStarted = savedInstanceState.getBoolean("pickerStarted", false);
        }

        if (!pickerStarted) {
            pickerStarted = true;
            openDocumentPicker();
        }
    }

    @Override
    protected void onSaveInstanceState(Bundle outState) {
        outState.putBoolean("pickerStarted", pickerStarted);
        super.onSaveInstanceState(outState);
    }

    private void openDocumentPicker() {
        String title = getIntent().getStringExtra("title");
        if (title == null || title.length() == 0) {
            title = "Model Seç";
        }

        Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        intent.setType("*/*");
        intent.putExtra(Intent.EXTRA_TITLE, title);
        intent.putExtra(Intent.EXTRA_MIME_TYPES, new String[] {
            "model/stl",
            "application/sla",
            "application/vnd.ms-pki.stl",
            "model/ply",
            "application/x-ply",
            "application/octet-stream"
        });
        intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);

        try {
            startActivityForResult(Intent.createChooser(intent, title), RequestCode);
        } catch (Exception ex) {
            sendFailure("Android dosya seçici açılamadı: " + ex.getMessage());
            finish();
        }
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);

        if (requestCode != RequestCode) {
            finish();
            return;
        }

        if (resultCode != RESULT_OK || data == null || data.getData() == null) {
            sendCancelled();
            finish();
            return;
        }

        importPickedFile(data);
        finish();
    }

    private void importPickedFile(Intent data) {
        Uri uri = data.getData();
        String displayName = getDisplayName(uri);
        if (displayName != null && displayName.length() > 0) {
            String lower = displayName.toLowerCase(Locale.ROOT);
            if (!lower.endsWith(".stl") && !lower.endsWith(".ply")) {
                sendFailure("Lütfen .stl veya .ply uzantılı bir dosya seçin.");
                return;
            }
        } else {
            displayName = "selected_model.stl";
        }

        try {
            int flags = data.getFlags() & Intent.FLAG_GRANT_READ_URI_PERMISSION;
            if (flags != 0) {
                getContentResolver().takePersistableUriPermission(uri, flags);
            }
        } catch (Exception ignored) {
            // Some providers do not expose persistable permissions. Reading immediately still works.
        }

        File outputDirectory = new File(getCacheDir(), "jaw_tracking_imports");
        if (!outputDirectory.exists() && !outputDirectory.mkdirs()) {
            sendFailure("Geçici model klasörü oluşturulamadı.");
            return;
        }

        File outputFile = new File(outputDirectory, System.currentTimeMillis() + "_" + sanitizeFileName(displayName));

        try (InputStream inputStream = getContentResolver().openInputStream(uri);
             FileOutputStream outputStream = new FileOutputStream(outputFile)) {
            if (inputStream == null) {
                sendFailure("Seçilen model dosyası açılamadı.");
                return;
            }

            byte[] buffer = new byte[64 * 1024];
            int read;
            while ((read = inputStream.read(buffer)) != -1) {
                outputStream.write(buffer, 0, read);
            }
        } catch (Exception ex) {
            sendFailure("Model dosyası okunamadı: " + ex.getMessage());
            return;
        }

        sendSuccess(outputFile.getAbsolutePath(), displayName);
    }

    private String getDisplayName(Uri uri) {
        try (Cursor cursor = getContentResolver().query(uri, null, null, null, null)) {
            if (cursor == null || !cursor.moveToFirst()) {
                return null;
            }

            int nameIndex = cursor.getColumnIndex(OpenableColumns.DISPLAY_NAME);
            if (nameIndex < 0) {
                return null;
            }

            return cursor.getString(nameIndex);
        } catch (Exception ignored) {
            return null;
        }
    }

    private static String sanitizeFileName(String fileName) {
        String sanitized = fileName.replaceAll("[\\\\/:*?\"<>|]", "_");
        String lower = sanitized.toLowerCase(Locale.ROOT);
        if (lower.endsWith(".stl") || lower.endsWith(".ply")) {
            return sanitized;
        }
        return sanitized + ".stl";
    }

    private static void sendSuccess(String path, String displayName) {
        sendPayload(true, false, path, displayName, "");
    }

    private static void sendCancelled() {
        sendPayload(false, true, "", "", "");
    }

    private static void sendFailure(String errorMessage) {
        sendPayload(false, false, "", "", errorMessage);
    }

    private static void sendPayload(boolean success, boolean cancelled, String path, String displayName, String errorMessage) {
        try {
            JSONObject payload = new JSONObject();
            payload.put("success", success);
            payload.put("cancelled", cancelled);
            payload.put("path", path);
            payload.put("displayName", displayName);
            payload.put("errorMessage", errorMessage);
            sendUnityMessage(payload.toString());
        } catch (Exception ex) {
            sendUnityMessage("{\"success\":false,\"cancelled\":false,\"path\":\"\",\"displayName\":\"\",\"errorMessage\":\"Android dosya seçici yanıtı oluşturulamadı.\"}");
        }
    }

    private static Activity getUnityActivity() {
        try {
            Class<?> unityPlayerClass = Class.forName("com.unity3d.player.UnityPlayer");
            Field currentActivityField = unityPlayerClass.getField("currentActivity");
            Object activity = currentActivityField.get(null);
            return activity instanceof Activity ? (Activity)activity : null;
        } catch (Exception ignored) {
            return null;
        }
    }

    private static void sendUnityMessage(String payload) {
        try {
            Class<?> unityPlayerClass = Class.forName("com.unity3d.player.UnityPlayer");
            Method sendMessageMethod = unityPlayerClass.getMethod(
                "UnitySendMessage",
                String.class,
                String.class,
                String.class
            );
            sendMessageMethod.invoke(null, CallbackObjectName, CallbackMethodName, payload);
        } catch (Exception ignored) {
            // The C# side will time out/cancel if Unity is unavailable.
        }
    }
}
