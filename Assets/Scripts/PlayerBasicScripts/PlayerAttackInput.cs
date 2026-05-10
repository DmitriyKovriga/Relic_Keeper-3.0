using System;
using System.Collections;
using System.Collections.Generic;
using Scripts.GameplayEvents;
using Scripts.Skills;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

[RequireComponent(typeof(PlayerSkillManager))]
[RequireComponent(typeof(PlayerMovement))]
public class PlayerAttackInput : MonoBehaviour
{
    private static readonly string[] SkillActionNames = { "FirstSkill", "SecondSkill", "ThirdSkill", "FourthSkill", "FifthSkill" };

    private enum DodgeGamepadButton
    {
        RightShoulder,
        LeftShoulder,
        South,
        North,
        East,
        West,
        LeftStick,
        RightStick,
        Start,
        Select,
        DpadUp,
        DpadDown,
        DpadLeft,
        DpadRight
    }

    [Header("Dodge")]
    [SerializeField] private float _dodgeTime = 0.24f;
    [SerializeField] private float _groundDodgeCooldown = 1f;
    [SerializeField] private float _airDodgeCooldown = 4f;
    [SerializeField] private float _airLandingRefundThreshold = 1f;
    [SerializeField] private float _groundDodgeDistance = 2.6f;
    [SerializeField] private float _airDodgeDistance = 2.6f;
    [SerializeField, Range(0f, 1f)] private float _dodgeVfxAlpha = 0.8f;
    [SerializeField] private Key _keyboardDodgeKey = Key.LeftShift;
    [SerializeField] private DodgeGamepadButton _gamepadDodgeButton = DodgeGamepadButton.RightShoulder;

    [Header("Dodge Feedback")]
    [SerializeField] private float _readyFlashDuration = 0.12f;
    [SerializeField] private Color _readyFlashColor = new(1f, 1f, 1f, 1f);

    [Header("Dash Jump")]
    [SerializeField, Min(1)] private int _dashJumpWindowMilliseconds = 160;
    [SerializeField, Min(0f)] private float _postDashJumpDodgeLockout = 0.2f;

    [Header("Directional Dodge Afterimage")]
    [SerializeField, Min(0.01f)] private float _afterImageInterval = 0.05f;
    [SerializeField, Min(0.05f)] private float _afterImageLifetime = 0.16f;
    [SerializeField, Range(0f, 1f)] private float _afterImageAlpha = 0.45f;
    [SerializeField] private Color _afterImageColor = new(0.1f, 0.1f, 0.1f, 0.45f);

    [Header("Directional Dodge Motion")]
    [SerializeField, Min(0f)] private float _directionalDodgeHorizontalCarryDuration = 0.35f;
    [SerializeField, Min(0.1f)] private float _directionalDodgeReleaseMomentumMultiplier = 1f;

    [Header("Stationary Dodge Pose")]
    [SerializeField, Range(0.7f, 1f)] private float _stationaryDodgeScaleY = 0.86f;

    private PlayerSkillManager _skillManager;
    private PlayerMovement _playerMovement;
    private Transform _visualRoot;
    private SpriteRenderer[] _playerRenderers = Array.Empty<SpriteRenderer>();
    private Sprite[] _moveDodgeFrames = Array.Empty<Sprite>();
    private Sprite[] _standDodgeFrames = Array.Empty<Sprite>();

    private readonly bool[] _pressedSkillInputs = new bool[5];
    private bool _isDodging;
    private bool _lastDodgeStartedInAir;
    private bool _landingRefundConsumed = true;
    private bool _readyFlashTriggered;
    private bool _wasGroundedLastFrame;
    private float _dodgeEndTime;
    private float _dodgeCooldownStartTime;
    private float _dodgeCooldownReadyTime;
    private Vector2 _savedVelocityBeforeDodge;
    private Coroutine _flashCoroutine;
    private Coroutine _afterImageCoroutine;
    private bool _isStationaryDodge;
    private bool _currentDodgeCanDashJump;
    private Vector3 _visualRootInitialLocalPosition;
    private Vector3 _visualRootInitialLocalScale;
    private float _visualRootBoundsHeight;
    private InputAction _dodgeAction;
    private float _dashJumpCancelWindowEndTime;
    private float _currentDodgeDirectionX;
    private float _dodgeLockedUntilTime;
    private bool _isDirectionalPhysicsDodge;
    private Vector2 _currentDodgeDirection;
    private float _currentDodgeDistance;
    private float _currentDodgeDuration;
    private Vector2 _currentDirectionalDodgeVelocity;

    private readonly InputAction[] _skillInputActions = new InputAction[5];
    private readonly Action<InputAction.CallbackContext>[] _skillStartedHandlers = new Action<InputAction.CallbackContext>[5];
    private readonly Action<InputAction.CallbackContext>[] _skillCanceledHandlers = new Action<InputAction.CallbackContext>[5];

    public bool IsDamageImmune => _isDodging;

    private void Awake()
    {
        _skillManager = GetComponent<PlayerSkillManager>();
        _playerMovement = GetComponent<PlayerMovement>();
        _visualRoot = transform.Find("Visuals");
        if (_visualRoot == null)
            _visualRoot = transform;

        RefreshPlayerRendererCache();
        _visualRootInitialLocalPosition = _visualRoot.localPosition;
        _visualRootInitialLocalScale = _visualRoot.localScale;
        _visualRootBoundsHeight = CalculateVisualBoundsHeight();
        _moveDodgeFrames = LoadOrderedSprites("VFX/Dodge/move_dodge");
        _standDodgeFrames = LoadOrderedSprites("VFX/Dodge/stand_dodge-Sheet");

        for (int i = 0; i < SkillActionNames.Length; i++)
        {
            int capturedIndex = i;
            _skillStartedHandlers[i] = _ => _pressedSkillInputs[capturedIndex] = true;
            _skillCanceledHandlers[i] = _ => _pressedSkillInputs[capturedIndex] = false;
        }

        _dodgeCooldownReadyTime = 0f;
        _wasGroundedLastFrame = _playerMovement != null && _playerMovement.IsGrounded;
    }

    private void OnEnable()
    {
        _dodgeAction = InputManager.InputActions.asset?.FindAction("Dodge", false);
        for (int i = 0; i < SkillActionNames.Length; i++)
        {
            InputAction action = InputManager.InputActions.asset?.FindAction(SkillActionNames[i], false);
            _skillInputActions[i] = action;
            if (action == null)
                continue;

            action.started += _skillStartedHandlers[i];
            action.canceled += _skillCanceledHandlers[i];
        }
    }

    private void OnDisable()
    {
        ClearPressedSkillInputs();

        if (InputManager.InputActions != null)
        {
            for (int i = 0; i < _skillInputActions.Length; i++)
            {
                InputAction action = _skillInputActions[i];
                if (action == null)
                    continue;

                action.started -= _skillStartedHandlers[i];
                action.canceled -= _skillCanceledHandlers[i];
                _skillInputActions[i] = null;
            }
        }

        if (_isDodging)
            FinishDodge();

        if (_flashCoroutine != null)
        {
            StopCoroutine(_flashCoroutine);
            _flashCoroutine = null;
        }

        if (_afterImageCoroutine != null)
        {
            StopCoroutine(_afterImageCoroutine);
            _afterImageCoroutine = null;
        }

        RestoreVisualPose();
    }

    private void Update()
    {
        UpdateDodgeState();

        if (_isDodging && TryConvertCurrentDodgeToDashJump())
            return;

        if (WasDodgePressedThisFrame())
        {
            if (TryStartDashJumpFromGroundInput())
                return;

            TryStartDodge();
            return;
        }

        if (_isDodging)
            return;

        for (int i = 0; i < _pressedSkillInputs.Length; i++)
        {
            if (_pressedSkillInputs[i])
                _skillManager.UseSkill(i);
        }
    }

    private void UpdateDodgeState()
    {
        bool groundedNow = _playerMovement != null && _playerMovement.IsGrounded;
        if (!_wasGroundedLastFrame && groundedNow)
            HandleLandingDuringCooldown();

        if (_isDodging && Time.time >= _dodgeEndTime)
            FinishDodge();

        if (_isDodging)
            UpdateDirectionalDodgeMotion();

        if (!_readyFlashTriggered && !_isDodging && Time.time >= _dodgeCooldownReadyTime)
            TriggerDodgeReadyFlash();

        _wasGroundedLastFrame = groundedNow;
    }

    private void ClearPressedSkillInputs()
    {
        for (int i = 0; i < _pressedSkillInputs.Length; i++)
            _pressedSkillInputs[i] = false;
    }

    private void TryStartDodge()
    {
        if (_isDodging)
            return;

        if (Time.time < _dodgeLockedUntilTime)
            return;

        if (Time.time < _dodgeCooldownReadyTime)
            return;

        _skillManager.CancelAllSkills();
        ClearPressedSkillInputs();

        bool startedGrounded = _playerMovement != null && _playerMovement.IsGrounded;
        Vector2 moveInput = _playerMovement != null ? _playerMovement.CurrentMoveInput : Vector2.zero;
        Vector2 dodgeDirection = GetDodgeDirection(moveInput, startedGrounded);
        float dodgeDistance = startedGrounded ? _groundDodgeDistance : _airDodgeDistance;
        float effectiveDodgeTime = Mathf.Max(0.01f, _dodgeTime);

        _savedVelocityBeforeDodge = _playerMovement != null ? _playerMovement.CurrentVelocity : Vector2.zero;
        bool stationaryDodge = dodgeDirection.sqrMagnitude <= 0.001f;
        _isDodging = true;
        _isStationaryDodge = stationaryDodge;
        _isDirectionalPhysicsDodge = !stationaryDodge;
        _currentDodgeDirection = dodgeDirection;
        _currentDodgeDistance = dodgeDistance;
        _currentDodgeDuration = effectiveDodgeTime;
        _currentDirectionalDodgeVelocity = BuildDirectionalDodgeVelocity(dodgeDirection, dodgeDistance, effectiveDodgeTime);
        _currentDodgeCanDashJump = startedGrounded && !_isStationaryDodge;
        _dashJumpCancelWindowEndTime = _currentDodgeCanDashJump ? Time.time + GetDashJumpWindowSeconds() : -1f;
        _currentDodgeDirectionX = Mathf.Abs(dodgeDirection.x) > 0.01f ? Mathf.Sign(dodgeDirection.x) : 0f;
        _lastDodgeStartedInAir = !startedGrounded;
        _landingRefundConsumed = startedGrounded;
        _readyFlashTriggered = false;
        _dodgeEndTime = Time.time + effectiveDodgeTime;
        _dodgeCooldownStartTime = Time.time;
        _dodgeCooldownReadyTime = Time.time + (startedGrounded ? _groundDodgeCooldown : _airDodgeCooldown);
        GameplayEventBus.Raise(GameplayEventType.Dodged, source: gameObject, target: gameObject);

        _skillManager.SetSkillUsageSuppressed(true);

        if (_playerMovement != null)
        {
            _playerMovement.SetMovementLock(true);
            if (_isDirectionalPhysicsDodge)
            {
                _playerMovement.BeginMotionOverride(
                    _currentDirectionalDodgeVelocity,
                    suspendGravity: true,
                    preserveVerticalVelocity: false);
            }
            else
            {
                _playerMovement.BeginMotionOverride(
                    Vector2.zero,
                    suspendGravity: true,
                    preserveVerticalVelocity: false);
            }

            if (Mathf.Abs(dodgeDirection.x) > 0.01f)
                _playerMovement.ForceFaceDirection(dodgeDirection.x);
        }

        ApplyDodgePose(_isStationaryDodge);
        SpawnDodgeVfx(_standDodgeFrames, effectiveDodgeTime, 2);
        if (!_isStationaryDodge)
        {
            SpawnDodgeVfx(_moveDodgeFrames, effectiveDodgeTime, 1);
            if (_afterImageCoroutine != null)
                StopCoroutine(_afterImageCoroutine);
            _afterImageCoroutine = StartCoroutine(SpawnAfterImages(effectiveDodgeTime));
        }
    }

    private void FinishDodge()
    {
        _isDodging = false;
        _isStationaryDodge = false;
        _currentDodgeCanDashJump = false;
        _dashJumpCancelWindowEndTime = -1f;
        _currentDodgeDirectionX = 0f;

        if (_playerMovement != null)
        {
            if (_isDirectionalPhysicsDodge)
            {
                Vector2 releaseVelocity = _currentDirectionalDodgeVelocity * Mathf.Max(0.1f, _directionalDodgeReleaseMomentumMultiplier);
                _playerMovement.EndMotionOverride(releaseVelocity);
                if (Mathf.Abs(releaseVelocity.x) > 0.01f && _directionalDodgeHorizontalCarryDuration > 0f)
                    _playerMovement.ApplyHorizontalMomentumCarry(releaseVelocity.x, _directionalDodgeHorizontalCarryDuration);
            }
            else
            {
                Vector2 restoredVelocity = _savedVelocityBeforeDodge;
                if (_playerMovement.IsGrounded)
                    restoredVelocity = new Vector2(restoredVelocity.x, Mathf.Min(0f, _playerMovement.CurrentVelocity.y));

                _playerMovement.EndMotionOverride(restoredVelocity);
            }

            _playerMovement.SetMovementLock(false);
        }

        _currentDodgeDirection = Vector2.zero;
        _currentDodgeDistance = 0f;
        _currentDodgeDuration = 0f;
        _currentDirectionalDodgeVelocity = Vector2.zero;

        if (_afterImageCoroutine != null)
        {
            StopCoroutine(_afterImageCoroutine);
            _afterImageCoroutine = null;
        }

        RestoreVisualPose();

        _skillManager.SetSkillUsageSuppressed(false);
        _isDirectionalPhysicsDodge = false;

        if (_playerMovement != null && _playerMovement.IsGrounded)
            HandleLandingDuringCooldown();

        if (!_readyFlashTriggered && Time.time >= _dodgeCooldownReadyTime)
            TriggerDodgeReadyFlash();
    }

    private void HandleLandingDuringCooldown()
    {
        if (!_lastDodgeStartedInAir || _landingRefundConsumed)
            return;

        if (Time.time >= _dodgeCooldownReadyTime)
        {
            _landingRefundConsumed = true;
            _lastDodgeStartedInAir = false;
            return;
        }

        if (Time.time - _dodgeCooldownStartTime < _airLandingRefundThreshold)
            return;

        _dodgeCooldownReadyTime = _isDodging ? _dodgeEndTime : Time.time;
        _landingRefundConsumed = true;
        _lastDodgeStartedInAir = false;
        _readyFlashTriggered = false;
    }

    private void TriggerDodgeReadyFlash()
    {
        _readyFlashTriggered = true;
        RefreshPlayerRendererCache();
        if (_playerRenderers == null || _playerRenderers.Length == 0)
            return;

        if (_flashCoroutine != null)
            StopCoroutine(_flashCoroutine);

        _flashCoroutine = StartCoroutine(PlayReadyFlash());
    }

    private bool TryStartDashJumpFromGroundInput()
    {
        if (_playerMovement == null || !_playerMovement.IsGrounded)
            return false;
        if (!_playerMovement.HasBufferedJump)
            return false;

        float direction = ResolveDashJumpDirection(_playerMovement.CurrentMoveInput, 0f);
        if (Mathf.Abs(direction) < 0.01f)
            return false;

        _playerMovement.ConsumeBufferedJump();
        _skillManager.CancelAllSkills();
        ClearPressedSkillInputs();
        return ExecuteDashJump(direction);
    }

    private bool TryConvertCurrentDodgeToDashJump()
    {
        if (!_currentDodgeCanDashJump)
            return false;
        if (Time.time > _dashJumpCancelWindowEndTime)
        {
            _currentDodgeCanDashJump = false;
            return false;
        }
        if (_playerMovement == null || !_playerMovement.HasBufferedJump)
            return false;

        float direction = ResolveDashJumpDirection(_playerMovement.CurrentMoveInput, _currentDodgeDirectionX);
        if (Mathf.Abs(direction) < 0.01f)
            return false;

        _playerMovement.ConsumeBufferedJump();
        return ExecuteDashJump(direction);
    }

    private bool ExecuteDashJump(float direction)
    {
        if (_playerMovement == null)
            return false;

        Vector2 carryVelocity = _playerMovement.CurrentVelocity;
        if (_isDodging)
        {
            _isDodging = false;
            _isStationaryDodge = false;
            _currentDodgeCanDashJump = false;
            _dashJumpCancelWindowEndTime = -1f;
            _currentDodgeDirectionX = 0f;
            _currentDodgeDirection = Vector2.zero;
            _currentDodgeDistance = 0f;
            _currentDodgeDuration = 0f;
            _currentDirectionalDodgeVelocity = Vector2.zero;

            if (_afterImageCoroutine != null)
            {
                StopCoroutine(_afterImageCoroutine);
                _afterImageCoroutine = null;
            }

            RestoreVisualPose();
            if (_isDirectionalPhysicsDodge)
                _playerMovement.EndMotionOverride(carryVelocity);
            else
                _playerMovement.EndMotionOverride(carryVelocity);
        }

        _playerMovement.SetMovementLock(false);
        _skillManager.SetSkillUsageSuppressed(false);
        _isDirectionalPhysicsDodge = false;

        if (!_playerMovement.TryPerformDashJump(direction))
            return false;

        _lastDodgeStartedInAir = false;
        _landingRefundConsumed = true;
        _dodgeCooldownStartTime = Time.time;
        _dodgeCooldownReadyTime = Time.time;
        _dodgeLockedUntilTime = Time.time + Mathf.Max(0f, _postDashJumpDodgeLockout);
        _readyFlashTriggered = true;
        return true;
    }

    private IEnumerator PlayReadyFlash()
    {
        for (int i = 0; i < _playerRenderers.Length; i++)
        {
            SpriteRenderer renderer = _playerRenderers[i];
            if (renderer == null || renderer.sprite == null || !renderer.enabled)
                continue;

            GameObject overlayObject = new($"DodgeReadyFlash_{i}");
            var overlay = overlayObject.AddComponent<TransientSpriteFlashOverlay>();
            overlay.Initialize(renderer, _readyFlashColor, _readyFlashDuration);
        }

        yield return new WaitForSeconds(Mathf.Max(0.01f, _readyFlashDuration));

        _flashCoroutine = null;
    }

    private void SpawnDodgeVfx(Sprite[] frames, float duration, int orderOffset)
    {
        if (frames == null || frames.Length == 0 || _visualRoot == null)
            return;

        GameObject go = new($"DodgeVfx_{orderOffset}");
        go.transform.SetParent(_visualRoot, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        var renderer = go.AddComponent<SpriteRenderer>();
        int sortingLayerId = renderer.sortingLayerID;
        int sortingOrder = renderer.sortingOrder;
        if (_playerRenderers != null && _playerRenderers.Length > 0)
        {
            sortingLayerId = _playerRenderers[0].sortingLayerID;
            sortingOrder = _playerRenderers[0].sortingOrder;
            for (int i = 0; i < _playerRenderers.Length; i++)
            {
                if (_playerRenderers[i] != null)
                    sortingOrder = Mathf.Max(sortingOrder, _playerRenderers[i].sortingOrder);
            }
        }

        var overlay = go.AddComponent<SpriteSheetOverlayVfx>();
        overlay.Initialize(frames, duration, _dodgeVfxAlpha, sortingLayerId, sortingOrder + orderOffset);
    }

    private void ApplyDodgePose(bool stationaryDodge)
    {
        if (_visualRoot == null)
            return;

        RestoreVisualPose();
        if (!stationaryDodge)
            return;

        float targetScaleY = Mathf.Clamp(_stationaryDodgeScaleY, 0.7f, 1f);
        float scaleLoss = 1f - targetScaleY;
        float offsetDown = _visualRootBoundsHeight * scaleLoss * 0.5f;

        _visualRoot.localScale = new Vector3(_visualRootInitialLocalScale.x, _visualRootInitialLocalScale.y * targetScaleY, _visualRootInitialLocalScale.z);
        _visualRoot.localPosition = _visualRootInitialLocalPosition + Vector3.down * offsetDown;
    }

    private void RestoreVisualPose()
    {
        if (_visualRoot == null)
            return;

        _visualRoot.localScale = _visualRootInitialLocalScale;
        _visualRoot.localPosition = _visualRootInitialLocalPosition;
    }

    private float CalculateVisualBoundsHeight()
    {
        if (_playerRenderers == null || _playerRenderers.Length == 0)
            return 1f;

        bool hasBounds = false;
        Bounds bounds = default;
        for (int i = 0; i < _playerRenderers.Length; i++)
        {
            SpriteRenderer renderer = _playerRenderers[i];
            if (renderer == null || renderer.sprite == null)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
            return 1f;

        Vector3 localMin = _visualRoot.InverseTransformPoint(bounds.min);
        Vector3 localMax = _visualRoot.InverseTransformPoint(bounds.max);
        return Mathf.Abs(localMax.y - localMin.y);
    }

    private void RefreshPlayerRendererCache()
    {
        var renderers = new List<SpriteRenderer>(8);
        var seen = new HashSet<SpriteRenderer>();

        SpriteRenderer rootRenderer = GetComponent<SpriteRenderer>();
        if (rootRenderer != null && seen.Add(rootRenderer))
            renderers.Add(rootRenderer);

        if (_visualRoot != null)
        {
            SpriteRenderer[] childRenderers = _visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < childRenderers.Length; i++)
            {
                SpriteRenderer childRenderer = childRenderers[i];
                if (childRenderer != null && seen.Add(childRenderer))
                    renderers.Add(childRenderer);
            }
        }

        _playerRenderers = renderers.ToArray();
    }

    private IEnumerator SpawnAfterImages(float dodgeDuration)
    {
        float elapsed = 0f;
        float interval = Mathf.Max(0.01f, _afterImageInterval);
        while (_isDodging && elapsed < dodgeDuration)
        {
            SpawnAfterImageSnapshot();
            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }

        _afterImageCoroutine = null;
    }

    private void SpawnAfterImageSnapshot()
    {
        if (_playerRenderers == null || _playerRenderers.Length == 0)
            return;

        GameObject ghostRoot = new("DodgeAfterImage");
        ghostRoot.transform.position = Vector3.zero;
        ghostRoot.transform.rotation = Quaternion.identity;
        ghostRoot.transform.localScale = Vector3.one;

        var color = new Color(_afterImageColor.r, _afterImageColor.g, _afterImageColor.b, Mathf.Clamp01(_afterImageAlpha));
        int childCount = 0;

        for (int i = 0; i < _playerRenderers.Length; i++)
        {
            SpriteRenderer source = _playerRenderers[i];
            if (source == null || !source.enabled || source.sprite == null)
                continue;

            GameObject child = new($"AfterImagePart_{i}");
            child.transform.SetParent(ghostRoot.transform, false);
            child.transform.position = source.transform.position;
            child.transform.rotation = source.transform.rotation;
            child.transform.localScale = source.transform.lossyScale;

            SpriteRenderer clone = child.AddComponent<SpriteRenderer>();
            clone.sprite = source.sprite;
            clone.flipX = source.flipX;
            clone.flipY = source.flipY;
            clone.sortingLayerID = source.sortingLayerID;
            clone.sortingOrder = source.sortingOrder - 1;
            clone.color = color;
            childCount++;
        }

        if (childCount == 0)
        {
            Destroy(ghostRoot);
            return;
        }

        var afterImage = ghostRoot.AddComponent<DodgeAfterImageGhost>();
        afterImage.Initialize(_afterImageLifetime);
    }

    private Vector2 GetDodgeDirection(Vector2 moveInput, bool startedGrounded)
    {
        if (startedGrounded)
        {
            if (Mathf.Abs(moveInput.x) < 0.1f)
                return Vector2.zero;

            return new Vector2(Mathf.Sign(moveInput.x), 0f);
        }

        if (moveInput.sqrMagnitude < 0.01f)
            return Vector2.zero;

        return moveInput.normalized;
    }

    private void UpdateDirectionalDodgeMotion()
    {
        if (!_isDodging || !_isDirectionalPhysicsDodge || _playerMovement == null)
            return;

        _playerMovement.UpdateMotionOverride(_currentDirectionalDodgeVelocity);
    }

    private static Vector2 BuildDirectionalDodgeVelocity(Vector2 dodgeDirection, float dodgeDistance, float dodgeTime)
    {
        if (dodgeDirection.sqrMagnitude < 0.0001f || dodgeDistance <= 0.0001f)
            return Vector2.zero;

        float speed = dodgeDistance / Mathf.Max(0.01f, dodgeTime);
        return dodgeDirection.normalized * speed;
    }

    private static float ResolveDashJumpDirection(Vector2 moveInput, float fallbackDirection)
    {
        if (Mathf.Abs(moveInput.x) >= 0.1f)
            return Mathf.Sign(moveInput.x);
        if (Mathf.Abs(fallbackDirection) >= 0.01f)
            return Mathf.Sign(fallbackDirection);
        return 0f;
    }

    private float GetDashJumpWindowSeconds()
    {
        return Mathf.Max(0.01f, _dashJumpWindowMilliseconds / 1000f);
    }

    private bool WasDodgePressedThisFrame()
    {
        if (_dodgeAction != null && _dodgeAction.WasPressedThisFrame())
            return true;

        if (Keyboard.current != null)
        {
            KeyControl keyControl = Keyboard.current[_keyboardDodgeKey];
            if (keyControl != null && keyControl.wasPressedThisFrame)
                return true;
        }

        if (Gamepad.current != null)
        {
            ButtonControl button = GetGamepadButton(Gamepad.current, _gamepadDodgeButton);
            if (button != null && button.wasPressedThisFrame)
                return true;
        }

        return false;
    }

    private static ButtonControl GetGamepadButton(Gamepad gamepad, DodgeGamepadButton button)
    {
        return button switch
        {
            DodgeGamepadButton.North => gamepad.buttonNorth,
            DodgeGamepadButton.South => gamepad.buttonSouth,
            DodgeGamepadButton.East => gamepad.buttonEast,
            DodgeGamepadButton.West => gamepad.buttonWest,
            DodgeGamepadButton.LeftShoulder => gamepad.leftShoulder,
            DodgeGamepadButton.RightShoulder => gamepad.rightShoulder,
            DodgeGamepadButton.LeftStick => gamepad.leftStickButton,
            DodgeGamepadButton.RightStick => gamepad.rightStickButton,
            DodgeGamepadButton.Start => gamepad.startButton,
            DodgeGamepadButton.Select => gamepad.selectButton,
            DodgeGamepadButton.DpadUp => gamepad.dpad.up,
            DodgeGamepadButton.DpadDown => gamepad.dpad.down,
            DodgeGamepadButton.DpadLeft => gamepad.dpad.left,
            DodgeGamepadButton.DpadRight => gamepad.dpad.right,
            _ => null
        };
    }

    private static Sprite[] LoadOrderedSprites(string resourcePath)
    {
        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
        if (sprites != null && sprites.Length > 0)
        {
            Array.Sort(sprites, (a, b) => string.CompareOrdinal(a.name, b.name));
            return sprites;
        }

        Sprite single = Resources.Load<Sprite>(resourcePath);
        if (single != null)
            return new[] { single };

        return Array.Empty<Sprite>();
    }
}

[RequireComponent(typeof(SpriteRenderer))]
public sealed class SpriteSheetOverlayVfx : MonoBehaviour
{
    private SpriteRenderer _renderer;
    private Sprite[] _frames;
    private float _duration;
    private float _elapsed;

    public void Initialize(Sprite[] frames, float duration, float alpha, int sortingLayerId, int sortingOrder)
    {
        _renderer = GetComponent<SpriteRenderer>();
        _frames = frames;
        _duration = Mathf.Max(0.01f, duration);
        _elapsed = 0f;

        if (_renderer == null)
            _renderer = gameObject.AddComponent<SpriteRenderer>();

        _renderer.sortingLayerID = sortingLayerId;
        _renderer.sortingOrder = sortingOrder;
        _renderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));

        if (_frames != null && _frames.Length > 0)
            _renderer.sprite = _frames[0];
    }

    private void Update()
    {
        if (_renderer == null || _frames == null || _frames.Length == 0)
        {
            Destroy(gameObject);
            return;
        }

        _elapsed += Time.deltaTime;
        float normalized = Mathf.Clamp01(_elapsed / _duration);
        int frameIndex = Mathf.Clamp(Mathf.FloorToInt(normalized * _frames.Length), 0, _frames.Length - 1);
        _renderer.sprite = _frames[frameIndex];

        if (_elapsed >= _duration)
            Destroy(gameObject);
    }
}

public sealed class DodgeAfterImageGhost : MonoBehaviour
{
    private SpriteRenderer[] _renderers;
    private Color[] _originalColors;
    private float _duration;
    private float _elapsed;

    public void Initialize(float duration)
    {
        _duration = Mathf.Max(0.01f, duration);
        _elapsed = 0f;
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        _originalColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
            _originalColors[i] = _renderers[i] != null ? _renderers[i].color : Color.white;
    }

    private void Update()
    {
        if (_renderers == null || _renderers.Length == 0)
        {
            Destroy(gameObject);
            return;
        }

        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);
        for (int i = 0; i < _renderers.Length; i++)
        {
            SpriteRenderer renderer = _renderers[i];
            if (renderer == null)
                continue;

            Color color = _originalColors[i];
            renderer.color = new Color(color.r, color.g, color.b, Mathf.Lerp(color.a, 0f, t));
        }

        if (_elapsed >= _duration)
            Destroy(gameObject);
    }
}

public sealed class TransientSpriteFlashOverlay : MonoBehaviour
{
    private SpriteRenderer _source;
    private SpriteRenderer _overlay;
    private Color _baseColor;
    private float _duration;
    private float _elapsed;

    public void Initialize(SpriteRenderer source, Color color, float duration)
    {
        _source = source;
        _baseColor = color;
        _duration = Mathf.Max(0.01f, duration);
        _elapsed = 0f;
        _overlay = gameObject.AddComponent<SpriteRenderer>();
        _overlay.color = color;
    }

    private void LateUpdate()
    {
        if (_source == null || _overlay == null)
        {
            Destroy(gameObject);
            return;
        }

        _elapsed += Time.deltaTime;
        if (_elapsed >= _duration)
        {
            Destroy(gameObject);
            return;
        }

        if (!_source.enabled || _source.sprite == null)
        {
            _overlay.enabled = false;
            return;
        }

        _overlay.enabled = true;
        _overlay.sprite = _source.sprite;
        _overlay.flipX = _source.flipX;
        _overlay.flipY = _source.flipY;
        _overlay.sortingLayerID = _source.sortingLayerID;
        _overlay.sortingOrder = _source.sortingOrder + 20;
        _overlay.maskInteraction = _source.maskInteraction;
        float alpha = 1f - Mathf.Clamp01(_elapsed / _duration);
        _overlay.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, alpha);
        transform.position = _source.transform.position;
        transform.rotation = _source.transform.rotation;
        transform.localScale = _source.transform.lossyScale;
    }
}

