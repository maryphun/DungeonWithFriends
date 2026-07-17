using System;
using UnityEngine;

namespace LayerLab.ArtMakerUnity
{
    /// <summary>
    /// Singleton player controller that manages character parts, game mode switching,
    /// and click-to-move behavior in Experience mode.
    /// </summary>
    public class Player : MonoBehaviour
    {
        private const string ANIM_IDLE = "Idle";
        private const string ANIM_WALK = "Walk";
        private const float MIN_DIRECTION_THRESHOLD = 0.01f;
        private const float ARRIVAL_DISTANCE = 0.1f;

        /// <summary>
        /// Singleton instance of the Player.
        /// </summary>
        public static Player Instance { get; private set; }

        [SerializeField] private PartsManager partsManager;
        [SerializeField] private Rigidbody2D rb;
        [SerializeField] private float moveSpeed = 3f;

        /// <summary>
        /// The PartsManager component used for character customization.
        /// </summary>
        public PartsManager PartsManager => partsManager;

        /// <summary>
        /// Invoked when the game mode changes. Parameter: the new game mode.
        /// </summary>
        public event Action<GameMode> OnModeChanged;

        private GameMode currentMode = GameMode.Home;
        private Vector2 moveTarget;
        private bool isMoving;
        private Camera _mainCamera;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _mainCamera = Camera.main;
        }

        /// <summary>
        /// Initializes the player by setting up the PartsManager and switching to Home mode.
        /// </summary>
        public void Init()
        {
            if (partsManager != null)
                partsManager.Init();

            SetMode(GameMode.Home);
        }

        /// <summary>
        /// Switches the current game mode, stopping movement and resetting animation.
        /// </summary>
        /// <param name="mode">The game mode to switch to.</param>
        public void SetMode(GameMode mode)
        {
            currentMode = mode;
            isMoving = false;

            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            if (mode == GameMode.Home && partsManager != null)
                partsManager.PlayAnimation(ANIM_IDLE);

            OnModeChanged?.Invoke(mode);
        }

        private void Update()
        {
            if (currentMode != GameMode.Experience) return;

            if (Input.GetMouseButtonDown(0))
            {
                HandleClick();
            }

            if (isMoving)
            {
                UpdateMovement();
            }
        }

        private void HandleClick()
        {
            Vector2 worldPos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
            moveTarget = worldPos;
            isMoving = true;

            if (partsManager != null)
                partsManager.PlayAnimation(ANIM_WALK);

            Vector3 scale = transform.localScale;
            float dir = moveTarget.x - transform.position.x;
            if (Mathf.Abs(dir) > MIN_DIRECTION_THRESHOLD)
                transform.localScale = new Vector3(Mathf.Sign(dir) * Mathf.Abs(scale.x), scale.y, scale.z);
        }

        private void UpdateMovement()
        {
            Vector2 current = transform.position;
            Vector2 direction = (moveTarget - current);
            float distance = direction.magnitude;

            if (distance < ARRIVAL_DISTANCE)
            {
                isMoving = false;
                if (rb != null)
                    rb.linearVelocity = Vector2.zero;

                if (partsManager != null)
                    partsManager.PlayAnimation(ANIM_IDLE);
                return;
            }

            if (rb != null)
                rb.linearVelocity = direction.normalized * moveSpeed;
        }
    }
}
