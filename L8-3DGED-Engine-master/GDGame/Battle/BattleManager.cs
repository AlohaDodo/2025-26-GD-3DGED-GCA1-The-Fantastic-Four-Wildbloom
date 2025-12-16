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
    // Player controls Bouldoise (press '2' to attack). AI controls Noodlord.
    public sealed class BattleManager
    {
        private sealed class Character
        {
            public string Name { get; set; } = string.Empty;
            public int HP { get; set; }
            public int Attack { get; set; }
            public bool IsPlayer { get; set; }
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

            SpawnCharacter(_player, new Vector3(26f, 0f, -30f), GetPreferredScale(_player.Name));
            SpawnCharacter(_ai, new Vector3(30f, 1f, 30f), GetPreferredScale(_ai.Name));

            SetStatus($"{_player.Name} vs {_ai.Name} — {_player.Name} to act. Press '2' to attack.");
        }


        private static float GetPreferredScale(string assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName))
                return 1.0f;

            switch (assetName.Trim())
            {
                case "Bouldoise":
                    return 1.2f;
                case "Noodlord":
                    return 1.3f;
                default:
                    return 1.25f;
            }
        }

        private void SpawnCharacter(Character ch, Vector3 worldPosition, float uniformScale = 1f)
        {
            var go = new GameObject(ch.Name);
            go.Transform.TranslateTo(worldPosition);
            go.Transform.ScaleTo(new Vector3(uniformScale, uniformScale, uniformScale));

            var model = _models.Get(ch.Name);
            MeshFilter meshFilter;
            if (model != null)
            {
                meshFilter = MeshFilterFactory.CreateFromModel(model, _graphics, 0, 0);
            }
            else
            {
                meshFilter = MeshFilterFactory.CreateCubeTexturedLit(_graphics);
            }

            go.AddComponent(meshFilter);

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

            Texture2D? tex = null;
            string[] textureCandidates = new[]
            {
                ch.Name,
                ch.Name + "TXT",
                ch.Name + "Tex",
                ch.Name + "_diff",
                ch.Name.ToLowerInvariant(),
            };

            foreach (var key in textureCandidates)
            {
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                tex = _textures.Get(key);
                if (tex != null)
                    break;
            }

            if (tex != null)
                meshRenderer.Overrides.MainTexture = tex;

            var box = go.AddComponent<BoxCollider>();
            box.Size = new Vector3(1f, 1f, 1f) * uniformScale;
            box.IsTrigger = true;

            var rb = go.AddComponent<RigidBody>();
            rb.BodyType = BodyType.Kinematic;
            rb.UseGravity = false;

            _scene.Add(go);

            ch.GameObject = go;
        }

        public void Update(GameTime gameTime)
        {
            if (_player.HP <= 0 || _ai.HP <= 0)
                return;

            var ks = Keyboard.GetState();

            if (_playersTurn)
            {
                if (ks.IsKeyDown(_attackKey) && !_prevKeyboard.IsKeyDown(_attackKey))
                {
                    DoAttack(_player, _ai);
                    _playersTurn = false;
                    _aiTimerSeconds = 0f;
                }
            }
            else
            {
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
            defender.HP -= attacker.Attack;
            if (defender.HP < 0)
                defender.HP = 0;

            var msg = $"{attacker.Name} attacked {defender.Name} for {attacker.Attack} damage. {defender.Name} HP: {defender.HP}";
            SetStatus(msg);
            Debug.WriteLine("[Battle] " + msg);

            if (defender.HP == 0)
            {
                var winMsg = $"{attacker.Name} defeated {defender.Name}!";
                SetStatus(winMsg);
                Debug.WriteLine("[Battle] " + winMsg);

                if (defender.GameObject != null)
                {
                    _scene.Remove(defender.GameObject);
                    defender.GameObject = null;
                }
            }
            else if (attacker.IsPlayer)
            {
                SetStatus($"{attacker.Name} attacked. {defender.Name} HP: {defender.    HP}. {defender} will act shortly.");
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