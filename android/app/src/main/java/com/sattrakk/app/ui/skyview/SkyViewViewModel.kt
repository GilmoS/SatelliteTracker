package com.sattrakk.app.ui.skyview

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.sattrakk.app.data.device.ArCompatibilityChecker
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject

// Sky View groundwork (Milestone E, Step 7): a device compatibility check only. No camera, no
// ARCore Session, no permission of any kind; see android/CLAUDE.md.
@HiltViewModel
class SkyViewViewModel @Inject constructor(
    private val arCompatibilityChecker: ArCompatibilityChecker,
) : ViewModel() {

    private val _uiState = MutableStateFlow<SkyViewUiState>(SkyViewUiState.Checking)
    val uiState: StateFlow<SkyViewUiState> = _uiState.asStateFlow()

    private var checkJob: Job? = null

    // Called by SkyViewScreen on every ON_RESUME (screen entry, tab return, back from the Play
    // Store), not from init: Sky View is a bottom-nav tab whose ViewModel survives tab switches,
    // and the ARCore APK can be installed or updated between visits. Never cached or persisted;
    // both checks are local and cheap. A previous Result stays on screen while re-checking, so a
    // tab return doesn't flash the spinner.
    fun checkCompatibility() {
        checkJob?.cancel()
        checkJob = viewModelScope.launch {
            val hasSensor = arCompatibilityChecker.hasRotationVectorSensor()
            val arCoreStatus = arCompatibilityChecker.checkArCore()
            _uiState.value = SkyViewUiState.Result(arCoreStatus, hasSensor)
        }
    }
}
