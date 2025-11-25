using GDEngine.Core;
using GDEngine.Core.Audio;
using GDEngine.Core.Audio.Events;
using GDEngine.Core.Collections;
using GDEngine.Core.Components;
using GDEngine.Core.Debug;
using GDEngine.Core.Entities;
using GDEngine.Core.Events;
using GDEngine.Core.Events.Types.Camera;
using GDEngine.Core.Factories;
using GDEngine.Core.Input.Data;
using GDEngine.Core.Input.Devices;
using GDEngine.Core.Rendering;
using GDEngine.Core.Rendering.Base;
using GDEngine.Core.Serialization;
using GDEngine.Core.Services;
using GDEngine.Core.Systems;
using GDEngine.Core.Timing;
using GDEngine.Core.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using Color = Microsoft.Xna.Framework.Color;

namespace GDGame
{
    public class Main : Game
    {
        #region Core Fields (Common to all games)     
        private GraphicsDeviceManager _graphics;
        private ContentDictionary<Texture2D> _textureDictionary;
        private ContentDictionary<Model> _modelDictionary;
        private ContentDictionary<SpriteFont> _fontDictionary;
        private ContentDictionary<SoundEffect> _soundDictionary;
        private ContentDictionary<Effect> _effectsDictionary;
        private Scene _scene;
        private bool _disposed = false;
        private Material _matBasicUnlit, _matBasicLit, _matAlphaCutout, _matBasicUnlitGround;
        #endregion

        #region Core Methods (Common to all games)     
        public Main()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
           
            #region Core

            // Give the game a name
            Window.Title = "Wildbloom";

            // Set resolution and centering (by monitor index)
            InitializeGraphics(ScreenResolution.R_HD_16_9_1280x720);

            // Center and hide the mouse!
            InitializeMouse();

            // Shared data across entities
            InitializeContext();

            // Assets from string names in JSON
            var relativeFilePathAndName = "assets/data/asset_manifest.json";
            LoadAssetsFromJSON(relativeFilePathAndName);

            // All effects used in game
            InitializeEffects();

            // Scene to hold game objects
            InitializeScene();

            // Camera, UI, Menu, Physics, Rendering etc.
            InitializeSystems();

            // All cameras we want in the game are loaded now and one set as active
            InitializeCameras();

            //game manager, camera changer, FSM, AI
            InitializeManagers();

            // Setup world
            int scale = 100;
            InitializeSkyParent();
            InitializeSkyBox(scale);
            {
                // 1. Create a new game object
                var ground = new GameObject("GroundPlane");

                // 2. Create tiled mesh (100x100 units, 20x20 UV tiles)
                var mesh = MeshFilterFactory.CreateQuadGridTexturedLit(
                    _graphics.GraphicsDevice,
                    widthSegments: 10,
                    heightSegments: 10,
                    width: 100f,
                    height: 100f,
                    uvTilesX: 20f,
                    uvTilesY: 20f
                );

                // 3. Attach MeshFilter component
                var meshFilter = ground.AddComponent<MeshFilter>();
                meshFilter.SetGeometry(
                    mesh.VertexBuffer,
                    mesh.IndexBuffer,
                    mesh.PrimitiveType,
                    mesh.IndexCount
                );

                // 4. Attach MeshRenderer component
                var meshRenderer = ground.AddComponent<MeshRenderer>();
                meshRenderer.Material = _matBasicUnlitGround;

                // 5. Apply ground texture
                meshRenderer.Overrides.MainTexture = _textureDictionary.Get("grass1");

                // 6. Ground transform
                ground.Transform.TranslateTo(new Vector3(0, -1, 0));
                ground.Transform.RotateEulerBy(new Vector3(MathHelper.ToRadians(-90), 0, 0));
                ground.Transform.ScaleTo(Vector3.One);

                // 7. Add to scene
                _scene.Add(ground);
            }

            // Setup renderers after all game objects added since ui text may use a gameobject as target
            InitializeUI();

            // Setup menu
            //InitializeMenu();

            #endregion

            base.Initialize();
        }

        private void InitializeManagers()
        {
            var go = new GameObject("Camera Manager");
            go.AddComponent<CameraChangeEventListener>();
            _scene.Add(go);
        }

        private void InitializeGraphics(Integer2 resolution)
        {
            // Enable per-monitor DPI awareness so the window/UI scales crisply on multi-monitor setups with different DPIs (avoids blurriness when moving between screens).
            System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);

            // Set preferred resolution
            ScreenResolution.SetResolution(_graphics, resolution);

            // Center on primary display (set to index of the preferred monitor)
            WindowUtility.CenterOnMonitor(this, 1);
        }

        private void InitializeMouse()
        {
            Mouse.SetPosition(_graphics.PreferredBackBufferWidth / 2, _graphics.PreferredBackBufferHeight / 2);

            
        }

        private void InitializeContext()
        {
            EngineContext.Initialize(GraphicsDevice, Content);
        }

        /// <summary>
        /// New asset loading from JSON using AssetEntry and ContentDictionary::LoadFromManifest
        /// </summary>
        /// <param name="relativeFilePathAndName"></param>
        /// <see cref="AssetEntry"/>
        /// <see cref="ContentDictionary{T}"/>
        private void LoadAssetsFromJSON(string relativeFilePathAndName)
        {
            // Make dictionaries to store assets
            _textureDictionary = new ContentDictionary<Texture2D>();
            _modelDictionary = new ContentDictionary<Model>();
            _fontDictionary = new ContentDictionary<SpriteFont>();
            _soundDictionary = new ContentDictionary<SoundEffect>();
            _effectsDictionary = new ContentDictionary<Effect>();
            //TODO - Add dictionary loading for other assets - song, other?

            var manifests = JSONSerializationUtility.LoadData<AssetManifest>(Content, relativeFilePathAndName); // single or array
            if (manifests.Count > 0)
            {
                foreach (var m in manifests)
                {
                    _modelDictionary.LoadFromManifest(m.Models, e => e.Name, e => e.ContentPath, overwrite: true);
                    _textureDictionary.LoadFromManifest(m.Textures, e => e.Name, e => e.ContentPath, overwrite: true);
                    _fontDictionary.LoadFromManifest(m.Fonts, e => e.Name, e => e.ContentPath, overwrite: true);
                    _soundDictionary.LoadFromManifest(m.Sounds, e => e.Name, e => e.ContentPath, overwrite: true);
                    _effectsDictionary.LoadFromManifest(m.Effects, e => e.Name, e => e.ContentPath, overwrite: true);
                    //TODO - Add dictionary loading for other assets - song, other?
                }
            }
        }

        private void InitializeEffects()
        {
            #region Unlit Textured BasicEffect 
            var unlitBasicEffect = new BasicEffect(_graphics.GraphicsDevice)
            {
                TextureEnabled = true,
                LightingEnabled = false,
                VertexColorEnabled = false
            };

            _matBasicUnlit = new Material(unlitBasicEffect);
            _matBasicUnlit.StateBlock = RenderStates.Opaque3D();      // depth on, cull CCW
            _matBasicUnlit.SamplerState = SamplerState.LinearClamp;   // helps avoid texture seams on sky

            //ground texture where UVs above [0,0]-[1,1]
            _matBasicUnlitGround = new Material(unlitBasicEffect.Clone());
            _matBasicUnlitGround.StateBlock = RenderStates.Opaque3D();      // depth on, cull CCW
            _matBasicUnlitGround.SamplerState = SamplerState.AnisotropicWrap;   // wrap texture based on UV values

            #endregion

            #region Lit Textured BasicEffect 
            var litBasicEffect = new BasicEffect(_graphics.GraphicsDevice)
            {
                TextureEnabled = true,
                LightingEnabled = true,
                PreferPerPixelLighting = true,
                VertexColorEnabled = false
            };
            litBasicEffect.EnableDefaultLighting();
            _matBasicLit = new Material(litBasicEffect);
            _matBasicLit.StateBlock = RenderStates.Opaque3D();
            #endregion

            #region Alpha-test for foliage/billboards
            var alphaFx = new AlphaTestEffect(GraphicsDevice)
            {
                VertexColorEnabled = false
            };
            _matAlphaCutout = new Material(alphaFx);

            // Depth test/write on; no blending (cutout happens in the effect). 
            // Make it two-sided so the quad is visible from both sides.
            _matAlphaCutout.StateBlock = RenderStates.Cutout3D()
                .WithRaster(new RasterizerState { CullMode = CullMode.None });

            // Clamp avoids edge bleeding from transparent borders.
            // (Use LinearWrap if the foliage textures tile.)
            _matAlphaCutout.SamplerState = SamplerState.LinearClamp;

            #endregion
        }

        private void InitializeScene()
        {
            // Make a scene that will store all drawn objects and systems for that level
            _scene = new Scene(EngineContext.Instance, "outdoors - level 1");
        }

        private void InitializeSystems()
        {
            InitializePhysicsSystem();
            InitializePhysicsDebugSystem(true);
            InitializeEventSystem();  //propagate events
            InitializeInputSystem();  //input
            InitializeCameraAndRenderSystems(); //update cameras, draw renderable game objects, draw ui and menu
            InitializeAudioSystem();
            
        }

        

        private void InitializeAudioSystem()
        {
            _scene.Add(new AudioSystem(_soundDictionary));
        }

        private void InitializePhysicsDebugSystem(bool isEnabled)
        {
            var physicsDebugRenderer = _scene.AddSystem(new PhysicsDebugRenderer());

            // Toggle debug rendering on/off
            physicsDebugRenderer.Enabled = isEnabled; // or false to hide

            // Optional: Customize colors
            physicsDebugRenderer.StaticColor = Color.Green;      // Immovable objects
            physicsDebugRenderer.KinematicColor = Color.Blue;    // Animated objects
            physicsDebugRenderer.DynamicColor = Color.Yellow;    // Physics-driven objects
            physicsDebugRenderer.TriggerColor = Color.Red;       // Trigger volumes

        }

        private void InitializePhysicsSystem()
        {
            // 1. add physics
            var physicsSystem = _scene.AddSystem(new PhysicsSystem());
            physicsSystem.Gravity = AppData.GRAVITY;
        }

        private void InitializeEventSystem()
        {
            _scene.Add(new EventSystem(EngineContext.Instance.Events));
        }

        private void InitializeCameraAndRenderSystems()
        {
            var cameraSystem = new CameraSystem(_graphics.GraphicsDevice, -100);
            _scene.Add(cameraSystem);

            var renderSystem = new RenderSystem(-100);
            _scene.Add(renderSystem);

            var uiRenderSystem = new UIRenderSystem(100);
            _scene.Add(uiRenderSystem); // draws in PostRender after RenderingSystem (order = -100)
        }

        private void InitializeInputSystem()
        {
            //set mouse, keyboard binding keys (e.g. WASD)
            var bindings = InputBindings.Default;
            // optional tuning
            bindings.MouseSensitivity = 0.12f;  // mouse look scale
            bindings.DebounceMs = 60;           // key/mouse debounce in ms
            bindings.EnableKeyRepeat = true;    // hold-to-repeat
            bindings.KeyRepeatMs = 300;         // repeat rate in ms

            // Create the input system 
            var inputSystem = new InputSystem();

            //register all the devices, you dont have to, but its for the demo
            inputSystem.Add(new GDKeyboardInput(bindings));
            inputSystem.Add(new GDMouseInput(bindings));
            inputSystem.Add(new GDGamepadInput(PlayerIndex.One, "Gamepad P1"));

            _scene.Add(inputSystem);
        }

        private void InitializeCameras()
        {
            GameObject cameraGO = null;
            Camera camera = null;
            
            #region First-person camera
            var position = new Vector3(0, 5, 25);

            //camera GO
            cameraGO = new GameObject(AppData.CAMERA_NAME_FIRST_PERSON);
            //set position 
            cameraGO.Transform.TranslateTo(position);
            //add camera component to the GO
            camera = cameraGO.AddComponent<Camera>();
            camera.FarPlane = 1000;
            ////feed off whatever screen dimensions you set InitializeGraphics
            camera.AspectRatio = (float)_graphics.PreferredBackBufferWidth / _graphics.PreferredBackBufferHeight;
            cameraGO.AddComponent<KeyboardWASDController>();
            cameraGO.AddComponent<MouseYawPitchController>();

            // Add it to the scene
            _scene.Add(cameraGO);
            #endregion
            //DO NOT CHANGE - First-person is default active camera
            _scene.SetActiveCamera(AppData.CAMERA_NAME_FIRST_PERSON);
        }

        /// <summary>
        /// Add parent root at origin to rotate the sky
        /// </summary>
        private void InitializeSkyParent()
        {
            var _skyParent = new GameObject("SkyParent");
            _scene.Add(_skyParent);
        }

        private void InitializeSkyBox(int scale = 500)
        {
            GameObject gameObject = null;
            MeshFilter meshFilter = null;
            MeshRenderer meshRenderer = null;

            // Find the sky parent object to attach sky to so sky rotates
            GameObject skyParent = _scene.Find((GameObject go) => go.Name.Equals("SkyParent"));

            // back
            gameObject = new GameObject("back");
            gameObject.Transform.ScaleTo(new Vector3(scale, scale, 1));
            gameObject.Transform.TranslateTo(new Vector3(0, 0, -scale / 2));
            meshFilter = MeshFilterFactory.CreateQuadTexturedLit(_graphics.GraphicsDevice);
            gameObject.AddComponent(meshFilter);
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.Material = _matBasicUnlit;
            meshRenderer.Overrides.MainTexture = _textureDictionary.Get("skybox_back");
            _scene.Add(gameObject);

            //set parent to allow rotation
            gameObject.Transform.SetParent(skyParent.Transform);

            // left
            gameObject = new GameObject("left");
            gameObject.Transform.ScaleTo(new Vector3(scale, scale, 1));
            gameObject.Transform.RotateEulerBy(new Vector3(0, MathHelper.ToRadians(90), 0), true);
            gameObject.Transform.TranslateTo(new Vector3(-scale / 2, 0, 0));
            meshFilter = MeshFilterFactory.CreateQuadTexturedLit(_graphics.GraphicsDevice);
            gameObject.AddComponent(meshFilter);
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.Material = _matBasicUnlit;
            meshRenderer.Overrides.MainTexture = _textureDictionary.Get("skybox_left");
            _scene.Add(gameObject);

            //set parent to allow rotation
            gameObject.Transform.SetParent(skyParent.Transform);


            // right
            gameObject = new GameObject("right");
            gameObject.Transform.ScaleTo(new Vector3(scale, scale, 1));
            gameObject.Transform.RotateEulerBy(new Vector3(0, MathHelper.ToRadians(-90), 0), true);
            gameObject.Transform.TranslateTo(new Vector3(scale / 2, 0, 0));
            meshFilter = MeshFilterFactory.CreateQuadTexturedLit(_graphics.GraphicsDevice);
            gameObject.AddComponent(meshFilter);
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.Material = _matBasicUnlit;
            meshRenderer.Overrides.MainTexture = _textureDictionary.Get("skybox_right");
            _scene.Add(gameObject);

            //set parent to allow rotation
            gameObject.Transform.SetParent(skyParent.Transform);

            // front
            gameObject = new GameObject("front");
            gameObject.Transform.ScaleTo(new Vector3(scale, scale, 1));
            gameObject.Transform.RotateEulerBy(new Vector3(0, MathHelper.ToRadians(180), 0), true);
            gameObject.Transform.TranslateTo(new Vector3(0, 0, scale / 2));
            meshFilter = MeshFilterFactory.CreateQuadTexturedLit(_graphics.GraphicsDevice);
            gameObject.AddComponent(meshFilter);
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.Material = _matBasicUnlit;
            meshRenderer.Overrides.MainTexture = _textureDictionary.Get("skybox_front");
            _scene.Add(gameObject);

            //set parent to allow rotation
            gameObject.Transform.SetParent(skyParent.Transform);

            // sky (top)
            gameObject = new GameObject("sky");
            gameObject.Transform.ScaleTo(new Vector3(scale, scale, 1));
            gameObject.Transform.RotateEulerBy(new Vector3(MathHelper.ToRadians(90), 0, MathHelper.ToRadians(90)), true);
            gameObject.Transform.TranslateTo(new Vector3(0, scale / 2, 0));
            meshFilter = MeshFilterFactory.CreateQuadTexturedLit(_graphics.GraphicsDevice);
            gameObject.AddComponent(meshFilter);
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.Material = _matBasicUnlit;
            meshRenderer.Overrides.MainTexture = _textureDictionary.Get("skybox_sky");
            _scene.Add(gameObject);

            //set parent to allow rotation
            gameObject.Transform.SetParent(skyParent.Transform);

        }

       private void InitializeUI()
        {
            InitializeUIReticleRenderer();
        }

        private void InitializeUIReticleRenderer()
        {
            var uiReticleGO = new GameObject("HUD");

            var reticleAtlas = _textureDictionary.Get("Crosshair_21");
            var uiFont = _fontDictionary.Get("mouse_reticle_font");

            // Reticle (cursor): always on top
            var reticle = new UIReticleRenderer(reticleAtlas);
            reticle.Origin = reticleAtlas.GetCenter();
            reticle.SourceRectangle = null;
            reticle.Scale = new Vector2(0.1f, 0.1f);
            reticle.RotationSpeedDegPerSec = 55;
            reticle.LayerDepth = UILayer.Cursor;
            uiReticleGO.AddComponent(reticle);

            var textRenderer = uiReticleGO.AddComponent<UITextRenderer>();
            textRenderer.Font = uiFont;         
            textRenderer.Offset = new Vector2(0, 30);  // Position text below reticle
            textRenderer.Color = Color.White;
            textRenderer.PositionProvider = () => _graphics.GraphicsDevice.Viewport.GetCenter();
            textRenderer.Anchor = TextAnchor.Center;
            
            var picker = uiReticleGO.AddComponent<UIPickerInfoRenderer>();
            picker.HitMask = LayerMask.All;
            picker.MaxDistance = 500f;
            picker.HitTriggers = false;

            // Optional custom formatting:
            picker.Formatter = hit =>
            {
                var go = hit.Body?.GameObject;
                if (go == null)
                    return string.Empty;

                return $"{go.Name}  d={hit.Distance:F1}";
            };

            _scene.Add(uiReticleGO);

            // Hide mouse since reticle will take its place
            IsMouseVisible = false;
        }

        
        protected override void Update(GameTime gameTime)
        {
            //call time update
            #region Core
            Time.Update(gameTime);

            //update Scene
            _scene.Update(Time.DeltaTimeSecs);
            #endregion

            

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Microsoft.Xna.Framework.Color.CornflowerBlue);

            //just as called update, we now have to call draw to call the draw in the renderingsystem
            _scene.Draw(Time.DeltaTimeSecs);

            base.Draw(gameTime);
        }

        /// <summary>
        /// Override Dispose to clean up engine resources.
        /// MonoGame's Game class already implements IDisposable, so we override its Dispose method.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                base.Dispose(disposing);
                return;
            }

            if (disposing)
            {
                System.Diagnostics.Debug.WriteLine("Disposing Main...");

                // 1. Dispose Scene (which will cascade to GameObjects and Components)
                System.Diagnostics.Debug.WriteLine("Disposing Scene");
                _scene?.Dispose();
                _scene = null;

                // 2. Dispose Materials (which may own Effects)
                System.Diagnostics.Debug.WriteLine("Disposing Materials");
                _matBasicUnlit?.Dispose();
                _matBasicUnlit = null;

                _matBasicLit?.Dispose();
                _matBasicLit = null;

                _matAlphaCutout?.Dispose();
                _matAlphaCutout = null;

                // 3. Clear cached MeshFilters in factory registry
                System.Diagnostics.Debug.WriteLine("Clearing MeshFilter Registry");
                MeshFilterFactory.ClearRegistry();

                // 4. Dispose content dictionaries (now they implement IDisposable!)
                System.Diagnostics.Debug.WriteLine("Disposing Content Dictionaries");
                _textureDictionary?.Dispose();
                _textureDictionary = null;

                _modelDictionary?.Dispose();
                _modelDictionary = null;

                _fontDictionary?.Dispose();
                _fontDictionary = null;

                // 5. Dispose EngineContext (which owns SpriteBatch and Content)
                System.Diagnostics.Debug.WriteLine("Disposing EngineContext");
                EngineContext.Instance?.Dispose();

                System.Diagnostics.Debug.WriteLine("Main disposal complete");
            }

            _disposed = true;

            // Always call base.Dispose
            base.Dispose(disposing);
        }

        #endregion    }
    }
}