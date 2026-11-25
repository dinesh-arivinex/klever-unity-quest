package com.klever.updates;

import android.app.Activity;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageInfo;
import android.content.pm.PackageInstaller;
import android.content.pm.PackageManager;
import android.content.pm.Signature;
import android.net.Uri;
import android.os.Build;
import android.provider.Settings;
import com.unity3d.player.UnityPlayer;
import java.io.File;
import java.io.FileInputStream;
import java.io.OutputStream;
import java.security.MessageDigest;

/** Ordinary Android installer. Does not invoke OEM services or claim silent privileges. */
public final class KleverPackageInstaller {
    static final String PREFS = "klever-update-install";
    private static volatile boolean busy;
    private static volatile boolean cancelled;
    private static volatile int activeSession = -1;
    private KleverPackageInstaller() {}

    public static boolean isAllowed(Activity activity) {
        return Build.VERSION.SDK_INT < 26 || activity.getPackageManager().canRequestPackageInstalls();
    }
    public static void openPermission(Activity activity) {
        activity.startActivity(new Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES,
                Uri.parse("package:" + activity.getPackageName())));
    }
    static void notifyUnity(String state) {
        try { UnityPlayer.UnitySendMessage("Klever verified app updates", "OnInstallerStatus", state); }
        catch (LinkageError | RuntimeException ignored) { /* Receiver may run before Unity's native library is loaded. */ }
    }
    private static String hex(byte[] value) {
        StringBuilder result = new StringBuilder();
        for (byte b : value) result.append(String.format(java.util.Locale.ROOT, "%02x", b & 255));
        return result.toString();
    }
    private static void verifyIdentity(Activity activity, File apk, String expectedPackage, int expectedVersion, String certificate) throws Exception {
        PackageManager manager = activity.getPackageManager();
        int flags = Build.VERSION.SDK_INT >= 28 ? PackageManager.GET_SIGNING_CERTIFICATES : PackageManager.GET_SIGNATURES;
        PackageInfo info = manager.getPackageArchiveInfo(apk.getPath(), flags);
        if (info == null || !activity.getPackageName().equals(expectedPackage) || !expectedPackage.equals(info.packageName)
                || info.versionCode != expectedVersion) throw new SecurityException("Archive identity mismatch");
        Signature[] signatures = Build.VERSION.SDK_INT >= 28 ? info.signingInfo.getApkContentsSigners() : info.signatures;
        if (signatures == null || signatures.length != 1 || !hex(MessageDigest.getInstance("SHA-256").digest(signatures[0].toByteArray())).equals(certificate))
            throw new SecurityException("Archive signer mismatch");
        PackageInfo installed = manager.getPackageInfo(expectedPackage, flags);
        Signature[] current = Build.VERSION.SDK_INT >= 28 ? installed.signingInfo.getApkContentsSigners() : installed.signatures;
        if (expectedVersion <= installed.versionCode || current == null || current.length != 1
                || !hex(MessageDigest.getInstance("SHA-256").digest(current[0].toByteArray())).equals(certificate))
            throw new SecurityException("Installed identity mismatch");
    }
    public static synchronized boolean begin(Activity activity, String file, String packageId, int version, String certificate, String expectedHash, long expectedBytes) {
        if (busy || !isAllowed(activity)) return false;
        busy = true; cancelled = false;
        new Thread(() -> {
            PackageInstaller installer = activity.getPackageManager().getPackageInstaller();
            int id = -1;
            boolean committed = false;
            try {
                File apk = new File(file).getCanonicalFile();
                File root = new File(activity.getExternalFilesDir(null), "app-updates").getCanonicalFile();
                if (!apk.getPath().startsWith(root.getPath() + File.separator) || !apk.isFile() || apk.length() != expectedBytes)
                    throw new SecurityException("Invalid update file");
                verifyIdentity(activity, apk, packageId, version, certificate);
                PackageInstaller.SessionParams params = new PackageInstaller.SessionParams(PackageInstaller.SessionParams.MODE_FULL_INSTALL);
                params.setAppPackageName(packageId); params.setSize(expectedBytes);
                if (Build.VERSION.SDK_INT >= 31) params.setRequireUserAction(PackageInstaller.SessionParams.USER_ACTION_REQUIRED);
                id = installer.createSession(params); activeSession = id;
                try (PackageInstaller.Session session = installer.openSession(id);
                     FileInputStream input = new FileInputStream(apk)) {
                    MessageDigest digest = MessageDigest.getInstance("SHA-256");
                    long copied = 0;
                    try (OutputStream output = session.openWrite("base.apk", 0, expectedBytes)) {
                        byte[] buffer = new byte[256 * 1024]; int count;
                        while ((count = input.read(buffer)) != -1) {
                            if (cancelled || copied + count > expectedBytes) throw new SecurityException("Update cancelled or changed");
                            digest.update(buffer, 0, count); output.write(buffer, 0, count); copied += count;
                        }
                        session.fsync(output);
                    }
                    if (cancelled || copied != expectedBytes || !hex(digest.digest()).equals(expectedHash))
                        throw new SecurityException("Update bytes mismatch");
                    activity.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit().putInt("targetVersion", version)
                            .putInt("session", id).putString("status", "committed").commit();
                    Intent callback = new Intent(activity, KleverUpdateStatusReceiver.class);
                    callback.setAction(activity.getPackageName() + ".KLEVER_UPDATE_RESULT");
                    callback.putExtra("expectedSession", id);
                    int pendingFlags = PendingIntent.FLAG_UPDATE_CURRENT;
                    if (Build.VERSION.SDK_INT >= 31) pendingFlags |= PendingIntent.FLAG_MUTABLE;
                    PendingIntent result = PendingIntent.getBroadcast(activity, id, callback, pendingFlags);
                    session.commit(result.getIntentSender()); committed = true;
                    notifyUnity("awaiting_android");
                }
            } catch (Exception error) {
                if (id >= 0 && !committed) { try { installer.abandonSession(id); } catch (Exception ignored) {} }
                busy = false; activeSession = -1;
                activity.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit().putString("status", "failed").commit();
                notifyUnity("failed");
            }
        }, "Klever APK staging").start();
        return true;
    }
    static void finished() { busy = false; activeSession = -1; }
    public static void cancel(Activity activity) {
        cancelled = true;
        int id = activeSession;
        if (id >= 0) { try { activity.getPackageManager().getPackageInstaller().abandonSession(id); } catch (Exception ignored) {} }
    }
    public static int confirmedVersion(Activity activity) {
        try {
            int target = activity.getSharedPreferences(PREFS, Context.MODE_PRIVATE).getInt("targetVersion", 0);
            int current = activity.getPackageManager().getPackageInfo(activity.getPackageName(), 0).versionCode;
            return target > 0 && current >= target ? current : 0;
        } catch (Exception ignored) { return 0; }
    }
}
