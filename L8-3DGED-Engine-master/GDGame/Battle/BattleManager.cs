using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using GDEngine.Core.Rendering.UI;
using GDEngine.Core.Rendering;
using GDEngine.Core.Entities;
using GDEngine.Core.Factories;
using GDEngine.Core.Collections;
using GDEngine.Core.Components;
using System;

namespace GDGame.Battle
{
    // Simple, self-contained turn-based manager for a two-character demo.
    // Player controls Bouldoise (press '2' to attack). AI controls Noodlord.
    public sealed class BattleManager
    {
        private sealed class Character
        {
            public string Name { get; set; } = string.Empty;
            public int HP { get; set; }
            public int Attack { get; set; }
            public bool IsPlayer { get; set; }

            // Link to the visual/gameobject spawned in the scene
            public GameObject? GameObject { get; set; }
        }

        private readonly Character _player;
        private readonly Character _ai;
        private readonly Scene _scene;
        private readonly ContentDictionary<Model> _models;
        private readonly ContentDictionary<Texture2D> _textures;
        private readonly GraphicsDevice _graphics;
        private bool _playersTurn;
        private KeyboardState _prevKeyboard;
        private readonly Keys _attackKey = Keys.D2;
        private float _aiDelaySeconds = 0.6f;
        private float _aiTimerSeconds;
        private readonly UIText? _statusText;

        /// <summary>
        /// Create a BattleManager that will spawn Bouldoise & Noodlord from the asset manifest
        /// (via provided content dictionaries) and attach simple colliders so they can be removed
        /// when their HP reaches zero.
        /// </summary>
        public BattleManager(
            Scene scene,
            ContentDictionary<Model> modelDictionary,
            ContentDictionary<Texture2D> textureDictionary,
            GraphicsDevice graphicsDevice,
            UIText? statusText)
        {
            _scene = scene ?? throw new ArgumentNullException(nameof(scene));
            _models = modelDictionary ?? throw new ArgumentNullException(nameof(modelDictionary));
            _textures = textureDictionary ?? throw new ArgumentNullException(nameof(textureDictionary));
            _graphics = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
            _statusText = statusText;

            _player = new Character { Name = "Bouldoise", HP = 30, Attack = 6, IsPlayer = true };
            _ai = new Character { Name = "Noodlord", HP = 30, Attack = 5, IsPlayer = false };
            _playersTurn = true;
            _prevKeyboard = Keyboard.GetState();

            // Spawn visual representations in the scene
            SpawnCharacter(_player, new Vector3(-2f, 0f, 0f), 1.25f);
            SpawnCharacter(_ai, new Vector3(2f, 0f, 0f), 1.25f);

            SetStatus($"{_player.Name} vs {_ai.Name} — {_player.Name} to act. Press '2' to attack.");
        }

        private void SpawnCharacter(Character ch, Vector3 worldPosition, float uniformScale = 1f)
        {
            // Create GameObject
            var go = new GameObject(ch.Name);
            go.Transform.TranslateTo(worldPosition);
            go.Transform.ScaleTo(new Vector3(uniformScale, uniformScale, uniformScale));

            // Try load model by key (fall back to simple cube quad if missing)
            var model = _models.Get(ch.Name);
            MeshFilter meshFilter;
            if (model != null)
            {
                // Use first mesh/part
                meshFilter = MeshFilterFactory.CreateFromModel(model, _graphics, 0, 0);
            }
            else
            {
                // No model entry found - create a textured cube as fallback
                meshFilter = MeshFilterFactory.CreateCubeTexturedLit(_graphics);
            }

            go.AddComponent(meshFilter);

            // Renderer + material
            var meshRenderer = go.AddComponent<MeshRenderer>();
            var litEffect = new BasicEffect(_graphics)
            {
                TextureEnabled = true,
                LightingEnabled = true,
                PreferPerPixelLighting = true,
                VertexColorEnabled = false
            };
            litEffect.EnableDefaultLighting();

            var mat = new Material(litEffect);
            meshRenderer.Material = mat;

            // Try apply texture if available
            var tex = _textures.Get(ch.Name);
            if (tex != null)
                meshRenderer.Overrides.MainTexture = tex;

            // Add a simple collider sized to the object scale (box)
            var box = go.AddComponent<BoxCollider>();
            // Use a reasonable default box size scaled by uniformScale
            box.Size = new Vector3(1f, 1f, 1f) * uniformScale;
            // Make trigger so it doesn't interfere physically (we only want events/visual)
            box.IsTrigger = true;

            // Add RigidBody (kinematic so it doesn't fall) - requires a collider earlier
            var rb = go.AddComponent<RigidBody>();
            rb.BodyType = BodyType.Kinematic;
            rb.UseGravity = false;

            // Register with scene (Awake/Start will run)
            _scene.Add(go);

            // Save link
            ch.GameObject = go;
        }

        public void Update(GameTime gameTime)
        {
            // If battle already decided, do nothing.
            if (_player.HP <= 0 || _ai.HP <= 0)
                return;

            var ks = Keyboard.GetState();

            if (_playersTurn)
            {
                // detect key-down edge for the '2' key
                if (ks.IsKeyDown(_attackKey) && !_prevKeyboard.IsKeyDown(_attackKey))
                {
                    DoAttack(_player, _ai);
                    _playersTurn = false;
                    _aiTimerSeconds = 0f;
                }
            }
            else
            {
                // AI waits a short delay before attacking to feel turn-based
                _aiTimerSeconds += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_aiTimerSeconds >= _aiDelaySeconds)
                {
                    DoAttack(_ai, _player);
                    _playersTurn = true;
                }
            }

            _prevKeyboard = ks;
        }

        private void DoAttack(Character attacker, Character defender)
        {
            // Apply damage
            defender.HP -= attacker.Attack;
            if (defender.HP < 0)
                defender.HP = 0;

            var msg = $"{attacker.Name} attacked {defender.Name} for {attacker.Attack} damage. {defender.Name} HP: {defender.HP}";
            SetStatus(msg);
            Debug.WriteLine("[Battle] " + msg);

            // If defender died, remove its GameObject (collider present, trigger semantics)
            if (defender.HP == 0)
            {
                var winMsg = $"{attacker.Name} defeated {defender.Name}!";
                SetStatus(winMsg);
                Debug.WriteLine("[Battle] " + winMsg);

                // Remove visual/gameobject from scene so it disappears
                if (defender.GameObject != null)
                {
                    // Defensive: only remove if still present in the scene
                    _scene.Remove(defender.GameObject);
                    defender.GameObject = null;
                }
            }
            else if (attacker.IsPlayer)
            {
                SetStatus($"{attacker.Name} attacked. {defender.Name} HP: {defender.    HP}. Noodlord will act shortly.");
            }
        }

        private void SetStatus(string text)
        {
            if (_statusText != null)
                _statusText.Text = text;
            Debug.WriteLine(text);
        }
    }
}