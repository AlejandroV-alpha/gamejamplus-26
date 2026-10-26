using System.Collections.Generic;
using GameJam.Combat;
using GameJam.Multiplayer;
using UnityEngine;

namespace GameJam.Inventory
{
    /// <summary>Cosmetic equipment and a blended, upper-body radio call pose. Never disables the player root.</summary>
    [DefaultExecutionOrder(12100)]
    public sealed class EquipmentPresentation : MonoBehaviour
    {
        [SerializeField] private GameObject radioVisualPrefab;
        [SerializeField, Tooltip("Posición local respecto a la mano derecha.")] private Vector3 radioGripOffset = new Vector3(-.092f,.413f,-.036f);
        [SerializeField, Tooltip("Rotación local respecto a la mano derecha.")] private Vector3 radioRotation = new Vector3(25.626f,-67.976f,53.75f);
        [SerializeField] private Vector3 radioScale = new Vector3(1.666664f,1.666664f,1.666664f);
        [SerializeField, Tooltip("Posición de la mano al hablar: derecha, altura y frente respecto a la cabeza, en metros.")]
        private Vector3 talkingHandOffset = new Vector3(.32f,-.42f,.30f);
        [SerializeField, Tooltip("Posición de la mano con el canal cerrado, respecto a la cabeza.")]
        private Vector3 carryingHandOffset = new Vector3(.17f,-.43f,.24f);
        [SerializeField, Range(.5f,.9f), Tooltip("Limita el alcance para mantener el codo doblado.")]
        private float maximumArmReach = .78f;
        [SerializeField, Min(.05f)] private float poseBlendSeconds = .2f;
        private PlayerEquipment equipment;
        private Transform weapon, upper, forearm, hand, head;
        private Animator animator;
        private GameObject radio;
        private Renderer led;
        private MaterialPropertyBlock ledColor;
        private readonly Dictionary<Transform,Quaternion> rest = new();
        private float weight;
        private void Awake()
        {
            ledColor = new MaterialPropertyBlock();
            equipment = GetComponent<PlayerEquipment>();
            animator = GetComponentInChildren<Animator>();
            weapon = GetComponent<PlayerInteractionAction>()?.weaponVisual;
            if (animator == null || !animator.isHuman) return;
            upper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            forearm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
        }
        private void Update() => RestoreArm();
        private void LateUpdate()
        {
            if (equipment == null) return;
            bool presenting = equipment.CanPresentItem;
            // weaponVisual is an explicitly configured child. Gun itself lives on the avatar.
            bool safeWeapon = weapon != null && weapon != transform && weapon.IsChildOf(transform)
                && (animator == null || (weapon != animator.transform && !animator.transform.IsChildOf(weapon)));
            if (safeWeapon)
            {
                bool visible = presenting && equipment.IsWeaponEquipped;
                if (weapon.gameObject.activeSelf != visible)
                {
                    weapon.gameObject.SetActive(visible);
                    if (!visible) GetComponent<Gun>()?.StopEquippedEffects();
                }
            }
            bool showRadio = presenting && equipment.IsRadioEquipped && hand != null && head != null;
            if (!showRadio)
            {
                if (radio != null) radio.SetActive(false);
                weight = 0;
                return;
            }
            if (radio == null && radioVisualPrefab != null)
            {
                radio = Instantiate(radioVisualPrefab, hand, false);
                radio.name = "Equipped walkie-talkie";
                foreach (var renderer in radio.GetComponentsInChildren<Renderer>())
                    if (renderer.name == "Channel LED") led = renderer;
            }
            if (radio == null) return;
            radio.SetActive(true);
            weight = Mathf.MoveTowards(weight,1,Time.deltaTime / Mathf.Max(.05f,poseBlendSeconds));
            // While the channel is closed, carry it in front of the chest. Opening raises it to the visor.
            var handOffset = equipment.IsRadioChannelOpen ? talkingHandOffset : carryingHandOffset;
            Vector3 target = head.position + transform.TransformDirection(handOffset);
            PoseArm(target,weight);
            // Grip calibration belongs to the hand's local space, including its animated rotation.
            radio.transform.localPosition = radioGripOffset;
            radio.transform.localRotation = Quaternion.Euler(radioRotation);
            radio.transform.localScale = radioScale;
            if (led != null)
            {
                ledColor.SetColor("_BaseColor", equipment.IsRadioChannelOpen ? new Color(.2f,1,.5f) : new Color(1,.55f,.15f));
                led.SetPropertyBlock(ledColor);
            }
        }
        private void PoseArm(Vector3 target,float blend)
        {
            if (upper == null || forearm == null || hand == null) return;
            rest[upper] = upper.localRotation; rest[forearm] = forearm.localRotation;
            Vector3 shoulder = upper.position;
            float a = Vector3.Distance(shoulder,forearm.position), b = Vector3.Distance(forearm.position,hand.position);
            Vector3 direction = target - shoulder;
            float distance = Mathf.Clamp(direction.magnitude,.01f,Mathf.Max(.01f,(a+b)*maximumArmReach));
            if (a < .001f || b < .001f || direction.sqrMagnitude < .0001f) return;
            direction.Normalize();
            target = shoulder + direction * distance;
            // Keep the elbow beside the ribs and below the shoulder, rather than pushing it outward.
            Vector3 pole = Vector3.ProjectOnPlane(-transform.up + transform.right*.2f,direction).normalized;
            float cosine = Mathf.Clamp((a*a+distance*distance-b*b)/(2*a*distance),-1,1);
            Vector3 elbow = shoulder + direction*(a*cosine) + pole*(a*Mathf.Sqrt(1-cosine*cosine));
            var upperDelta = Quaternion.FromToRotation(forearm.position-shoulder,elbow-shoulder);
            upper.rotation = Quaternion.Slerp(Quaternion.identity,upperDelta,blend)*upper.rotation;
            var lowerDelta = Quaternion.FromToRotation(hand.position-forearm.position,target-forearm.position);
            forearm.rotation = Quaternion.Slerp(Quaternion.identity,lowerDelta,blend)*forearm.rotation;
        }
        private void RestoreArm()
        {
            foreach (var pair in rest) if (pair.Key != null) pair.Key.localRotation = pair.Value;
            rest.Clear();
        }
        private void OnDisable()
        {
            RestoreArm(); if (radio != null) radio.SetActive(false);
        }
        private void OnDestroy()
        {
            RestoreArm(); if (radio != null) Destroy(radio);
        }
    }
}
