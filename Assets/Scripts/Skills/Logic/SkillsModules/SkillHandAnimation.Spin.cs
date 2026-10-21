using Scripts.Visuals;
using UnityEngine;

namespace Scripts.Skills.Modules
{
    public partial class SkillHandAnimation
    {
        private object _spinToken;
        private Vector3 _spinWeaponPosition;
        private Quaternion _spinWeaponRotation;
        private Vector3 _spinWeaponScale;
        private float _spinTipLength, _spinSpriteTilt;
        private bool _spinWeaponEnabled;
        private bool _spinFlipX, _spinFlipY;
        private int _spinWeaponLayer, _spinWeaponOrder;
        private WorldDepthSort _spinDepth;
        private bool _spinDepthEnabled;
        private Transform _spinRig, _spinOriginalParent;
        private int _spinSibling;
        private Vector3 _spinStartPosition;
        private Quaternion _spinStartRotation;

        public void SampleSpin(object token, float phase, float radius, float squash, float centerHeight, float turnProgress = -1)
        {
            if (token == null || _weaponRenderer == null || _ownerStats == null || _anchor == null) return;
            if (!ReferenceEquals(_spinToken, token))
            {
                EndSpin(_spinToken);
                _anchor.Acquire(this);
                _spinToken = token;
                _spinWeaponPosition = _weaponRenderer.transform.localPosition;
                _spinWeaponRotation = _weaponRenderer.transform.localRotation;
                _spinWeaponScale = _weaponRenderer.transform.localScale;
                var bounds = _weaponRenderer.sprite != null ? _weaponRenderer.sprite.bounds : new Bounds();
                Vector2 tipDirection = Quaternion.Euler(0, 0, _spinSpriteTilt) * Vector3.up;
                _spinTipLength = Mathf.Max(0, Vector2.Dot(bounds.center, tipDirection)
                    + Mathf.Abs(tipDirection.x) * bounds.extents.x + Mathf.Abs(tipDirection.y) * bounds.extents.y)
                    * Mathf.Abs(_weaponRenderer.transform.lossyScale.y);
                _spinWeaponEnabled = _weaponRenderer.enabled;
                _spinFlipX = _weaponRenderer.flipX; _spinFlipY = _weaponRenderer.flipY;
                _weaponRenderer.flipX = _weaponRenderer.flipY = false;
                _spinWeaponLayer = _weaponRenderer.sortingLayerID;
                _spinWeaponOrder = _weaponRenderer.sortingOrder;
                _spinDepth = _weaponRenderer.GetComponent<WorldDepthSort>();
                _spinDepthEnabled = _spinDepth != null && _spinDepth.enabled;
                if (_spinDepth != null) _spinDepth.enabled = false;
                _spinOriginalParent = _weaponRenderer.transform.parent;
                _spinSibling = _weaponRenderer.transform.GetSiblingIndex();
                _spinStartPosition = _weaponRenderer.transform.position;
                _spinStartRotation = _weaponRenderer.transform.rotation;
                Vector3 worldScale = _weaponRenderer.transform.lossyScale;
                _spinRig = new GameObject("Skill spin grip").transform;
                _spinRig.SetParent(_handPivot != null ? _handPivot.parent : _ownerStats.transform, false);
                _weaponRenderer.transform.SetParent(_spinRig, false);
                Vector3 ownerScale = _spinRig.parent.lossyScale;
                _weaponRenderer.transform.localScale = new Vector3(Mathf.Abs(worldScale.x / ownerScale.x), Mathf.Abs(worldScale.y / ownerScale.y), _spinWeaponScale.z);
            }
            float angle = -phase * Mathf.PI * 2;
            float facing = Mathf.Sign(_ownerStats.transform.lossyScale.x);
            float reach = Mathf.Clamp(radius - _spinTipLength, .1f, .35f);
            float shaftScale = _spinTipLength > .001f ? Mathf.Max(.01f, radius - reach) / _spinTipLength : 1;
            Vector3 offset = new Vector3(Mathf.Cos(angle) * reach * facing, Mathf.Sin(angle) * reach * squash, 0);
            Vector3 orbitPosition = _ownerStats.transform.position + Vector3.up * centerHeight + offset;
            float projection = Mathf.Sqrt(Mathf.Pow(Mathf.Cos(angle), 2) + Mathf.Pow(Mathf.Sin(angle) * squash, 2));
            float blend = 1;
            bool settling = turnProgress > .86f;
            if (turnProgress >= 0)
                blend = Mathf.SmoothStep(0, 1, settling ? (1 - turnProgress) / .14f : turnProgress / .14f);
            Vector3 posePosition = _spinStartPosition;
            Quaternion poseRotation = _spinStartRotation;
            if (settling && _handPivot != null && _handPivot.parent != null)
            {
                posePosition = _handPivot.parent.TransformPoint(_defaultPos + _defaultRot * _spinWeaponPosition);
                poseRotation = _handPivot.parent.rotation * _defaultRot * _spinWeaponRotation;
            }
            _spinRig.position = Vector3.Lerp(posePosition, orbitPosition, blend);
            _spinRig.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg - 90);
            // Project the shaft's length before rotating the painted artwork. Blade width stays intact.
            _spinRig.localScale = new Vector3(1, Mathf.Lerp(1, projection * shaftScale, blend), 1);
            _weaponRenderer.transform.localPosition = Vector3.zero;
            _weaponRenderer.transform.localRotation = Quaternion.Euler(0, 0, -_spinSpriteTilt);
            if (blend < 1) _weaponRenderer.transform.rotation = Quaternion.Slerp(poseRotation, _weaponRenderer.transform.rotation, blend);
            _weaponRenderer.enabled = _weaponRenderer.sprite != null;
            var body = _ownerStats.GetComponent<SpriteRenderer>();
            if (body != null)
            {
                _weaponRenderer.sortingLayerID = body.sortingLayerID;
                _weaponRenderer.sortingOrder = body.sortingOrder + (Mathf.Sin(angle) > .05f ? -2 : 2);
            }
        }

        public void EndSpin(object token)
        {
            if (token == null || !ReferenceEquals(_spinToken, token)) return;
            if (_weaponRenderer != null)
            {
                _weaponRenderer.transform.SetParent(_spinOriginalParent, false);
                _weaponRenderer.transform.SetSiblingIndex(_spinSibling);
                _weaponRenderer.transform.localPosition = _spinWeaponPosition;
                _weaponRenderer.transform.localRotation = _spinWeaponRotation;
                _weaponRenderer.transform.localScale = _spinWeaponScale;
                _weaponRenderer.enabled = _spinWeaponEnabled;
                _weaponRenderer.flipX = _spinFlipX; _weaponRenderer.flipY = _spinFlipY;
                _weaponRenderer.sortingLayerID = _spinWeaponLayer;
                _weaponRenderer.sortingOrder = _spinWeaponOrder;
            }
            if (_spinRig != null)
            {
                if (Application.isPlaying) Destroy(_spinRig.gameObject); else DestroyImmediate(_spinRig.gameObject);
                _spinRig = null;
            }
            if (_spinDepth != null) _spinDepth.enabled = _spinDepthEnabled;
            if (_handPivot != null && _anchor != null && _anchor.IsOwnedBy(this))
            {
                _handPivot.localPosition = _defaultPos; _handPivot.localRotation = _defaultRot;
            }
            _spinToken = null;
        }
    }
}
