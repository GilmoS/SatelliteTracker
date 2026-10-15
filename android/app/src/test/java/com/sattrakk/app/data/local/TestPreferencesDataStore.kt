package com.sattrakk.app.data.local

import androidx.datastore.core.DataStore
import androidx.datastore.core.okio.OkioStorage
import androidx.datastore.preferences.core.PreferenceDataStoreFactory
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.PreferencesSerializer
import java.io.File
import okio.FileSystem
import okio.Path.Companion.toOkioPath

// JVM-test DataStore for the DataStore-backed stores' round-trip tests. Built on OkioStorage
// explicitly rather than PreferenceDataStoreFactory.create(produceFile = ...): as of datastore
// 1.1.7 (pulled in transitively by the Firebase BoM, up from the 1.1.1 this project pins), the
// File-based factory writes through FileStorage, which commits each write with File.renameTo —
// and on a Windows dev machine renameTo refuses to replace an existing target, so every write
// after the first failed with "Unable to rename ... multiple instances of DataStore". Okio's
// atomicMove replaces the target on every platform. Production is unaffected (Android's rename
// overwrites); this only changes how the tests' DataStore commits to disk, not the serialized
// Preferences format the stores under test read and write.
fun testPreferencesDataStore(file: File): DataStore<Preferences> =
    PreferenceDataStoreFactory.create(
        storage = OkioStorage(FileSystem.SYSTEM, PreferencesSerializer) { file.toOkioPath() }
    )

// createTempFile only reserves a unique name; the file itself is deleted so DataStore starts empty.
fun newTempPreferencesFile(prefix: String): File =
    File.createTempFile(prefix, ".preferences_pb").apply { delete() }
