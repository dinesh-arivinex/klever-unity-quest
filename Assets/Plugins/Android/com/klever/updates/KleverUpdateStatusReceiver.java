package com.klever.updates;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.pm.PackageInstaller;

/** Non-exported explicit PackageInstaller result receiver. */
public final class KleverUpdateStatusReceiver extends BroadcastReceiver {
    @Override public void onReceive(Context context, Intent intent) {
        SharedPreferences state = context.getSharedPreferences(KleverPackageInstaller.PREFS, Context.MODE_PRIVATE);
        int session = intent.getIntExtra(PackageInstaller.EXTRA_SESSION_ID, -1);
        if (session < 0 || session != state.getInt("session", -2)) return;
        int status = intent.getIntExtra(PackageInstaller.EXTRA_STATUS, PackageInstaller.STATUS_FAILURE);
        if (status == PackageInstaller.STATUS_PENDING_USER_ACTION) {
            Intent confirmation = intent.getParcelableExtra(Intent.EXTRA_INTENT);
            if (confirmation != null) {
                try {
                    confirmation.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                    context.startActivity(confirmation);
                    KleverPackageInstaller.notifyUnity("awaiting_android");
                    return;
                } catch (Exception ignored) {}
            }
            state.edit().putString("status", "failed").commit();
            KleverPackageInstaller.finished(); KleverPackageInstaller.notifyUnity("failed");
            return;
        }
        KleverPackageInstaller.finished();
        boolean success = status == PackageInstaller.STATUS_SUCCESS;
        state.edit().putString("status", success ? "installed" : "failed").putInt("resultCode", status).commit();
        KleverPackageInstaller.notifyUnity(success ? "installed" : "failed");
        if (success) {
            Intent launch = context.getPackageManager().getLaunchIntentForPackage(context.getPackageName());
            if (launch != null) {
                try { launch.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK); context.startActivity(launch); }
                catch (Exception ignored) { /* A platform may require reopening from its launcher. */ }
            }
        }
    }
}
