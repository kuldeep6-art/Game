using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using MyEngine;
using System.Collections.Generic;
using System;
using Vector3 = MyEngine.Vector3;
using Quaternion = MyEngine.Quaternion;
using System.Linq;
using MyGame;

namespace MyGame
{
    public class PhysicsManager
    {
        private readonly ParticleWorld _physicsWorld;
        private readonly BrickBreakerGame _game;
        private float deltaTime;

        public PhysicsManager(ParticleWorld physicsWorld, BrickBreakerGame game)
        {
            _physicsWorld = physicsWorld;
            _game = game;
        }

        public void UpdateMechanics(List<Ball> balls, Paddle paddle, KeyboardState keyboardState, float deltaTime, ref int shardStormCharge, ref float paddleChargeTimer, ref float vacuumFieldTimer, ref float gravityWellTimer, ref Vector3? gravityWellPosition, List<Brick> bricks, List<Particle> particles, List<Fragment> fragments, Random random)
        {
            this.deltaTime = deltaTime;
            if (keyboardState.IsKeyDown(Keys.Space))
            {
                foreach (var ball in balls)
                {
                    if (IsBallNearPaddle(ball, paddle))
                        ball.PhysicsBody.Velocity.Y -= 200f * deltaTime;
                    if (vacuumFieldTimer > 0)
                    {
                        Vector3 direction = paddle.Position - ball.Position;
                        direction.normalize();
                        ball.PhysicsBody.Velocity += direction * 300f * deltaTime;
                    }
                }
            }
            else if (keyboardState.IsKeyDown(Keys.LeftShift))
            {
                foreach (var ball in balls)
                {
                    if (IsBallNearPaddle(ball, paddle))
                        ball.PhysicsBody.Velocity.Y += 200f * deltaTime;
                }
            }

            if (keyboardState.IsKeyDown(Keys.C))
            {
                paddleChargeTimer += deltaTime;
                if (paddleChargeTimer >= 2f)
                {
                    ReleaseChargeShot(paddle, bricks, particles, fragments, random, _game._gameManager);
                    paddleChargeTimer = 0f;
                }
            }
            else
            {
                paddleChargeTimer = Math.Max(0, paddleChargeTimer - deltaTime * 2);
            }

            if (keyboardState.IsKeyDown(Keys.S) && shardStormCharge >= 3)
            {
                ActivateShardStorm(paddle, bricks, particles, fragments, random, _game._gameManager);
                shardStormCharge = 0;
            }
        }

        public void UpdatePowerUps(List<PowerUp> powerUps, Paddle paddle, float deltaTime, int screenHeight, GameManager gameManager, List<Ball> balls, ParticleWorld physicsWorld, Random random, Texture2D ballTexture)
        {
            this.deltaTime = deltaTime;
            for (int i = powerUps.Count - 1; i >= 0; i--)
            {
                powerUps[i].Position.Y += 100f * deltaTime;
                if (powerUps[i].Position.Y > screenHeight)
                {
                    powerUps.RemoveAt(i);
                }
                else if (CheckPowerUpCollection(powerUps[i], paddle))
                {
                    ApplyPowerUp(powerUps[i].Type, gameManager, balls, physicsWorld, random, ballTexture);
                    powerUps.RemoveAt(i);
                }
            }
        }

        public void UpdateEffects(List<Particle> particles, List<Fragment> fragments, Boss boss, float deltaTime, int screenHeight, ref float paddleEnlargeTimer, ref bool isPaddleEnlarged, ref Paddle paddle, Texture2D originalPaddleTexture, ref float enhancedManeuverabilityTimer, List<Ball> balls, ref float vacuumFieldTimer, ref float gravityWellTimer, ref Vector3? gravityWellPosition)
        {
            this.deltaTime = deltaTime;
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                particles[i].Update(deltaTime);
                if (particles[i].Life <= 0)
                    particles.RemoveAt(i);
            }

            for (int i = fragments.Count - 1; i >= 0; i--)
            {
                fragments[i].Update(deltaTime);
                if (fragments[i].Life <= 0 || fragments[i].Position.Y > screenHeight)
                    fragments.RemoveAt(i);
            }

            boss?.Update(deltaTime);

            if (isPaddleEnlarged)
            {
                paddleEnlargeTimer -= deltaTime;
                if (paddleEnlargeTimer <= 0)
                {
                    paddle.Texture = originalPaddleTexture;
                    isPaddleEnlarged = false;
                }
            }

            if (enhancedManeuverabilityTimer > 0)
            {
                enhancedManeuverabilityTimer -= deltaTime;
                foreach (var ball in balls)
                {
                    if (ball.PhysicsBody.Velocity.Magnitude() > 200f)
                        ball.PhysicsBody.Velocity *= 0.95f;
                }
            }

            if (vacuumFieldTimer > 0)
                vacuumFieldTimer -= deltaTime;

            if (gravityWellTimer > 0)
            {
                gravityWellTimer -= deltaTime;
                if (gravityWellTimer <= 0)
                    gravityWellPosition = null;
                foreach (var ball in balls)
                {
                    if (gravityWellPosition != new Vector3())
                    {
                        Vector3 direction = gravityWellPosition - ball.Position;
                        float distance = direction.Magnitude();
                        direction.normalize();
                        float force = MathHelper.Clamp(5000f / (distance * distance), 0, 500f);
                        ball.PhysicsBody.Velocity += direction * force * deltaTime;
                    }
                }
            }
        }

        public void UpdatePaddle(Paddle paddle, KeyboardState keyboardState, float deltaTime, int screenWidth)
        {
            this.deltaTime = deltaTime;
            float paddleSpeed = 500f;
            Vector3 paddleVelocity = new Vector3(0, 0, 0);

            if (keyboardState.IsKeyDown(Keys.Left))
                paddleVelocity.X = -paddleSpeed;
            else if (keyboardState.IsKeyDown(Keys.Right))
                paddleVelocity.X = paddleSpeed;

            paddle.PhysicsBody.Velocity = paddleVelocity;
            paddle.PhysicsBody.Position += paddleVelocity * deltaTime;
            paddle.PhysicsBody.Position.X = MathHelper.Clamp(paddle.Position.X, paddle.Width / 2, screenWidth - paddle.Width / 2);
            paddle.PhysicsBody.TransformMatrix.SetOrientationAndPos(paddle.PhysicsBody.Orientation, paddle.PhysicsBody.Position);
        }


        private bool IsBallNearPaddle(Ball ball, Paddle paddle)
        {
            float distance = ball.Position.CalculateDistance(ball.Position, paddle.Position);
            return distance < 100f;
        }

        private void ActivateShardStorm(Paddle paddle, List<Brick> bricks, List<Particle> particles, List<Fragment> fragments, Random random, GameManager gameManager)
        {
            float paddleX = paddle.Position.X;
            foreach (var brick in bricks.ToList())
            {
                if (Math.Abs(brick.Position.X - paddleX) < 50)
                {
                    bricks.Remove(brick);
                    _physicsWorld.RemoveBody(brick.PhysicsBody);
                    AddParticles(brick.Position, 10, particles, random);
                    AddFragments(brick.Position, fragments, random);
                    gameManager.AddScore(5);
                }
            }
            Console.WriteLine("SHARDSTORM activated!");
        }

        private void ReleaseChargeShot(Paddle paddle, List<Brick> bricks, List<Particle> particles, List<Fragment> fragments, Random random, GameManager gameManager)
        {
            foreach (var brick in bricks.ToList())
            {
                if (Math.Abs(brick.Position.X - paddle.Position.X) < 30 && brick.Position.Y < paddle.Position.Y)
                {
                    brick.Health -= 3;
                    if (brick.Health <= 0)
                    {
                        bricks.Remove(brick);
                        _physicsWorld.RemoveBody(brick.PhysicsBody);
                        gameManager.AddScore(15);
                        AddParticles(brick.Position, 10, particles, random);
                        AddFragments(brick.Position, fragments, random);
                    }
                    else
                    {
                        brick.Texture = _game.CreateBrickTexture(_game.GraphicsDevice, (int)brick.Width, (int)brick.Height, brick.BaseColor, brick.Health);
                    }
                }
            }
            Console.WriteLine("Charge Shot released!");
        }


        private bool CheckPowerUpCollection(PowerUp powerUp, Paddle paddle)
        {
            Rectangle powerUpRect = new Rectangle((int)(powerUp.Position.X - 15), (int)(powerUp.Position.Y - 15), 30, 30);
            Rectangle paddleRect = new Rectangle((int)(paddle.Position.X - paddle.Width / 2), (int)(paddle.Position.Y - paddle.Height / 2), (int)paddle.Width, (int)paddle.Height);
            return powerUpRect.Intersects(paddleRect);
        }

        private void ApplyPowerUp(PowerUpType type, GameManager gameManager, List<Ball> balls, ParticleWorld physicsWorld, Random random, Texture2D ballTexture)
        {
            switch (type)
            {
                case PowerUpType.MultiBall:
                    Ball newBall = new Ball();
                    newBall.Texture = ballTexture;
                    newBall.PhysicsBody.InverseMass = 1f;
                    newBall.PhysicsBody.linearDamping = 0.99f;
                    newBall.PhysicsBody.Velocity = new Vector3((float)(random.NextDouble() - 0.5) * 400, -300, 0);
                    newBall.PhysicsBody.SetInertiaTensor(_game.CreateSphereInertiaTensor(newBall.Radius, 1f));
                    newBall.PhysicsBody.isAwake = true;
                    balls.Add(newBall);
                    physicsWorld.AddBody(newBall.PhysicsBody);
                    Console.WriteLine("Multi-Ball activated!");
                    break;
                case PowerUpType.ExtraLife:
                    gameManager.GainLife();
                    Console.WriteLine("Extra life gained!");
                    break;
                case PowerUpType.EnhancedManeuverability:
                    gameManager.EnhancedManeuverabilityTimer = 10f;
                    Console.WriteLine("Enhanced maneuverability for 10 seconds!");
                    break;
                case PowerUpType.ShardStormCharge:
                    gameManager.ShardStormCharge++;
                    Console.WriteLine($"ShardStorm charge: {gameManager.ShardStormCharge}/3");
                    break;
                case PowerUpType.VacuumField:
                    gameManager.VacuumFieldTimer = 5f;
                    Console.WriteLine("Vacuum Field activated for 5 seconds!");
                    break;
                case PowerUpType.GravityWell:
                    gameManager.GravityWellPosition = new Vector3(BrickBreakerGame.SCREEN_WIDTH / 2, BrickBreakerGame.SCREEN_HEIGHT / 2, 0);
                    gameManager.GravityWellTimer = 5f;
                    Console.WriteLine("Gravity Well activated for 5 seconds!");
                    break;
            }
        }


        public void CheckCollisions(List<Ball> balls, List<Brick> bricks, Paddle paddle, Boss boss, List<RigidBody> walls, List<Fragment> fragments, List<Particle> particles, Random random, List<PowerUp> powerUps, int screenHeight, GameManager gameManager, ParticleWorld physicsWorld, Func<GraphicsDevice, int, int, Color, int, Texture2D> createBrickTexture, Func<GraphicsDevice, PowerUpType, Texture2D> createPowerUpTexture, ref Boss bossRef)
        {
            if (gameManager.GameOver) return;

            _physicsWorld.ClearContacts();
            _physicsWorld.StartFrameB();

            foreach (var body in _physicsWorld.GetBodies())
            {
                if (body == null) continue;
                body.Integrate(deltaTime);
                body.CalculateDerivedData();
                Matrix3 newInertiaTensorWorld = new Matrix3();
                RigidBody.TransformInertiaTensor(ref newInertiaTensorWorld, body.Orientation, body.InverseInertiaTensor, body.TransformMatrix);
                body.inverseInertiaTensorWorld = newInertiaTensorWorld;
                body.ClearAccumulators();
            }

            List<Ball> ballsToRemove = new List<Ball>();

            foreach (var ball in balls)
            {
                foreach (var brick in bricks.ToList())
                {
                    if (CheckBallBrickCollision(ball, brick))
                    {
                        brick.Health--;
                        if (brick.Health <= 0)
                        {
                            bricks.Remove(brick);
                            physicsWorld.RemoveBody(brick.PhysicsBody);
                            gameManager.AddScore(10);
                            AddParticles(brick.Position, 10, particles, random);
                            AddFragments(brick.Position, fragments, random);
                            if (random.NextDouble() < 0.2)
                            {
                                PowerUpType type = (PowerUpType)random.Next(0, 6);
                                PowerUp powerUp = new PowerUp(type, brick.Position, createPowerUpTexture(_game.GraphicsDevice, type));
                                powerUps.Add(powerUp);
                            }
                        }
                        else
                        {
                            brick.Texture = createBrickTexture(_game.GraphicsDevice, (int)brick.Width, (int)brick.Height, brick.BaseColor, brick.Health);
                        }
                    }
                }

                CheckBallPaddleCollision(ball, paddle);

                if (boss != null && CheckBallBossCollision(ball, boss))
                {
                    boss.Health -= 10;
                    if (boss.Health <= 0)
                    {
                        physicsWorld.RemoveBody(boss.PhysicsBody);
                        bossRef = null;
                        gameManager.AddScore(100);
                        gameManager.NextLevel();
                        _game.SetupLevel();
                    }
                }

                foreach (var wall in walls)
                {
                    if (wall.Position.Y > screenHeight && ball.Position.Y + ball.Radius > screenHeight)
                    {
                        ballsToRemove.Add(ball);
                    }
                    else
                    {
                        CheckBallWallCollision(ball, wall);
                    }
                }
            }

            foreach (var fragment in fragments.ToList())
            {
                foreach (var brick in bricks.ToList())
                {
                    if (CheckFragmentBrickCollision(fragment, brick))
                    {
                        brick.Health--;
                        if (brick.Health <= 0)
                        {
                            bricks.Remove(brick);
                            physicsWorld.RemoveBody(brick.PhysicsBody);
                            gameManager.AddScore(5);
                            AddParticles(brick.Position, 5, particles, random);
                        }
                        else
                        {
                            brick.Texture = createBrickTexture(_game.GraphicsDevice, (int)brick.Width, (int)brick.Height, brick.BaseColor, brick.Health);
                        }
                        fragments.Remove(fragment);
                    }
                }
                if (boss != null && CheckFragmentBossCollision(fragment, boss))
                {
                    boss.Health -= 5;
                    fragments.Remove(fragment);
                    if (boss.Health <= 0)
                    {
                        physicsWorld.RemoveBody(boss.PhysicsBody);
                        bossRef = null;
                        gameManager.AddScore(100);
                        gameManager.NextLevel();
                        _game.SetupLevel();
                    }
                }
            }

            foreach (var ballToRemove in ballsToRemove)
            {
                balls.Remove(ballToRemove);
                physicsWorld.RemoveBody(ballToRemove.PhysicsBody);
            }

            if (balls.Count == 0 && ballsToRemove.Count > 0)
            {
                gameManager.LoseLife();
                if (!gameManager.GameOver)
                {
                    Ball newBall = new Ball();
                    newBall.Texture = _game.CreateBallTexture(_game.GraphicsDevice, 20);
                    newBall.PhysicsBody.InverseMass = 1f;
                    newBall.PhysicsBody.linearDamping = 0.99f;
                    newBall.PhysicsBody.Velocity = new Vector3(200, -300, 0);
                    newBall.PhysicsBody.SetInertiaTensor(_game.CreateSphereInertiaTensor(newBall.Radius, 1f));
                    newBall.PhysicsBody.isAwake = true;
                    balls.Add(newBall);
                    physicsWorld.AddBody(newBall.PhysicsBody);
                    Console.WriteLine($"Lives remaining: {gameManager.Lives}");
                }
            }

            if (bricks.Count == 0 && boss == null && gameManager.CurrentMode == GameMode.Story)
            {
                gameManager.NextLevel();
                _game.SetupLevel();
            }

            foreach (var contact in _physicsWorld.CurrentFrameContacts)
            {
                contact.CalculateInternals(deltaTime);
                contact.Resolve(deltaTime);
            }

            gameManager.CheckAchievements(bricks.Count, gameManager.Score);
        }

        private bool CheckBallBrickCollision(Ball ball, Brick brick)
        {
            if (ball?.PhysicsBody?.Position == null || brick?.PhysicsBody?.Position == null)
                return false;

            float ballRadius = ball.Radius;
            float brickHalfWidth = brick.Width / 2f;
            float brickHalfHeight = brick.Height / 2f;

            Vector3 delta = ball.PhysicsBody.Position - brick.PhysicsBody.Position;
            float intersectX = Math.Abs(delta.X) - (ballRadius + brickHalfWidth);
            float intersectY = Math.Abs(delta.Y) - (ballRadius + brickHalfHeight);

            if (intersectX < 0 && intersectY < 0)
            {
                Vector3 normal = delta;
                normal.normalize();
                var contact = new ParticleContact
                {
                    Body = new RigidBody[2] { ball.PhysicsBody, brick.PhysicsBody },
                    ContactNormal = normal,
                    penetration = -Math.Min(intersectX, intersectY),
                    Restitution = 1.0f,
                    Friction = 0.1f,
                    contactPoint = ball.PhysicsBody.Position,
                    RelativeContactPosition = new Vector3[2] { new Vector3(), new Vector3() },
                    InverseInertiaTensor = new Matrix3[2]
                    {
                        ball.PhysicsBody.InverseInertiaTensor ?? new Matrix3(),
                        brick.PhysicsBody.InverseInertiaTensor ?? new Matrix3()
                    }
                };
                _physicsWorld.AddContact(contact);
                return true;
            }
            return false;
        }

        private bool CheckBallBossCollision(Ball ball, Boss boss)
        {
            if (ball?.PhysicsBody?.Position == null || boss?.PhysicsBody?.Position == null)
                return false;

            float ballRadius = ball.Radius;
            float bossHalfWidth = boss.Width / 2f;
            float bossHalfHeight = boss.Height / 2f;

            Vector3 delta = ball.PhysicsBody.Position - boss.PhysicsBody.Position;
            float intersectX = Math.Abs(delta.X) - (ballRadius + bossHalfWidth);
            float intersectY = Math.Abs(delta.Y) - (ballRadius + bossHalfHeight);

            if (intersectX < 0 && intersectY < 0)
            {
                Vector3 normal = delta;
                normal.normalize();
                var contact = new ParticleContact
                {
                    Body = new RigidBody[2] { ball.PhysicsBody, boss.PhysicsBody },
                    ContactNormal = normal,
                    penetration = -Math.Min(intersectX, intersectY),
                    Restitution = 1.0f,
                    Friction = 0.1f,
                    contactPoint = ball.PhysicsBody.Position,
                    RelativeContactPosition = new Vector3[2] { new Vector3(), new Vector3() },
                    InverseInertiaTensor = new Matrix3[2]
                    {
                        ball.PhysicsBody.InverseInertiaTensor ?? new Matrix3(),
                        boss.PhysicsBody.InverseInertiaTensor ?? new Matrix3()
                    }
                };
                _physicsWorld.AddContact(contact);
                return true;
            }
            return false;
        }

        private void CheckBallPaddleCollision(Ball ball, Paddle paddle)
        {
            if (ball?.PhysicsBody?.Position == null || paddle?.PhysicsBody?.Position == null)
                return;

            float ballRadius = ball.Radius;
            float paddleHalfWidth = paddle.Width / 2f;
            float paddleHalfHeight = paddle.Height / 2f;

            Vector3 delta = ball.PhysicsBody.Position - paddle.PhysicsBody.Position;
            float intersectX = Math.Abs(delta.X) - (ballRadius + paddleHalfWidth);
            float intersectY = Math.Abs(delta.Y) - (ballRadius + paddleHalfHeight);

            if (intersectX < 0 && intersectY < 0)
            {
                Vector3 normal = new Vector3(0, -1, 0);
                if (ball.PhysicsBody.Velocity.Y > 0)
                {
                    var contact = new ParticleContact
                    {
                        Body = new RigidBody[2] { ball.PhysicsBody, paddle.PhysicsBody },
                        ContactNormal = normal,
                        penetration = -intersectY,
                        Restitution = 1.0f,
                        Friction = 0.1f,
                        contactPoint = ball.PhysicsBody.Position,
                        RelativeContactPosition = new Vector3[2] { new Vector3(), new Vector3() },
                        InverseInertiaTensor = new Matrix3[2]
                        {
                            ball.PhysicsBody.InverseInertiaTensor ?? new Matrix3(),
                            paddle.PhysicsBody.InverseInertiaTensor ?? new Matrix3()
                        }
                    };
                    _physicsWorld.AddContact(contact);

                    float relativeHitPos = (ball.Position.X - paddle.Position.X) / paddle.Width;
                    ball.PhysicsBody.Velocity.X = relativeHitPos * 400f;
                }
            }
        }

        private void CheckBallWallCollision(Ball ball, RigidBody wall)
        {
            if (ball?.PhysicsBody?.Position == null || wall?.Position == null)
                return;

            float ballRadius = ball.Radius;

            if (wall.Position.X < 0)
            {
                if (ball.Position.X - ballRadius < 0)
                {
                    var contact = new ParticleContact
                    {
                        Body = new RigidBody[2] { ball.PhysicsBody, wall },
                        ContactNormal = new Vector3(1, 0, 0),
                        penetration = ballRadius - ball.Position.X,
                        Restitution = 1.0f,
                        Friction = 0.1f,
                        contactPoint = ball.PhysicsBody.Position,
                        RelativeContactPosition = new Vector3[2] { new Vector3(), new Vector3() },
                        InverseInertiaTensor = new Matrix3[2]
                        {
                            ball.PhysicsBody.InverseInertiaTensor ?? new Matrix3(),
                            wall.InverseInertiaTensor ?? new Matrix3()
                        }
                    };
                    _physicsWorld.AddContact(contact);
                    ball.PhysicsBody.Position.X = ballRadius;
                }
            }
            else if (wall.Position.X > BrickBreakerGame.SCREEN_WIDTH)
            {
                if (ball.Position.X + ballRadius > BrickBreakerGame.SCREEN_WIDTH)
                {
                    var contact = new ParticleContact
                    {
                        Body = new RigidBody[2] { ball.PhysicsBody, wall },
                        ContactNormal = new Vector3(-1, 0, 0),
                        penetration = ball.Position.X + ballRadius - BrickBreakerGame.SCREEN_WIDTH,
                        Restitution = 1.0f,
                        Friction = 0.1f,
                        contactPoint = ball.PhysicsBody.Position,
                        RelativeContactPosition = new Vector3[2] { new Vector3(), new Vector3() },
                        InverseInertiaTensor = new Matrix3[2]
                        {
                            ball.PhysicsBody.InverseInertiaTensor ?? new Matrix3(),
                            wall.InverseInertiaTensor ?? new Matrix3()
                        }
                    };
                    _physicsWorld.AddContact(contact);
                    ball.PhysicsBody.Position.X = BrickBreakerGame.SCREEN_WIDTH - ballRadius;
                }
            }
            else if (wall.Position.Y < 0)
            {
                if (ball.Position.Y - ballRadius < 0)
                {
                    var contact = new ParticleContact
                    {
                        Body = new RigidBody[2] { ball.PhysicsBody, wall },
                        ContactNormal = new Vector3(0, 1, 0),
                        penetration = ballRadius - ball.Position.Y,
                        Restitution = 1.0f,
                        Friction = 0.1f,
                        contactPoint = ball.PhysicsBody.Position,
                        RelativeContactPosition = new Vector3[2] { new Vector3(), new Vector3() },
                        InverseInertiaTensor = new Matrix3[2]
                        {
                            ball.PhysicsBody.InverseInertiaTensor ?? new Matrix3(),
                            wall.InverseInertiaTensor ?? new Matrix3()
                        }
                    };
                    _physicsWorld.AddContact(contact);
                    ball.PhysicsBody.Position.Y = ballRadius;
                    if (ball.PhysicsBody.Velocity.Y < 0)
                        ball.PhysicsBody.Velocity.Y = -ball.PhysicsBody.Velocity.Y;
                }
            }
        }

        private bool CheckFragmentBrickCollision(Fragment fragment, Brick brick)
        {
            float fragRadius = fragment.Radius;
            float brickHalfWidth = brick.Width / 2f;
            float brickHalfHeight = brick.Height / 2f;

            Vector3 delta = fragment.Position - brick.PhysicsBody.Position;
            float intersectX = Math.Abs(delta.X) - (fragRadius + brickHalfWidth);
            float intersectY = Math.Abs(delta.Y) - (fragRadius + brickHalfHeight);

            return intersectX < 0 && intersectY < 0;
        }

        private bool CheckFragmentBossCollision(Fragment fragment, Boss boss)
        {
            float fragRadius = fragment.Radius;
            float bossHalfWidth = boss.Width / 2f;
            float bossHalfHeight = boss.Height / 2f;

            Vector3 delta = fragment.Position - boss.PhysicsBody.Position;
            float intersectX = Math.Abs(delta.X) - (fragRadius + bossHalfWidth);
            float intersectY = Math.Abs(delta.Y) - (fragRadius + bossHalfHeight);

            return intersectX < 0 && intersectY < 0;
        }

        private void AddParticles(Vector3 position, int count, List<Particle> particles, Random random)
        {
            for (int i = 0; i < count; i++)
                particles.Add(new Particle(position, random, _game.GraphicsDevice));
        }

        private void AddFragments(Vector3 position, List<Fragment> fragments, Random random)
        {
            for (int i = 0; i < 3; i++)
                fragments.Add(new Fragment(position, random, _game.GraphicsDevice));
        }
    }

	public class GameManager
	{
		private readonly BrickBreakerGame _game;
		private readonly object _achievementLock = new object();
		
		public GameManager(BrickBreakerGame game)
		{
			_game = game;
			_achievements = new Dictionary<string, Achievement>
			{
				{ "FirstBlood", new Achievement("First Blood", "Break your first brick") },
				{ "ChainBreaker", new Achievement("Chain Breaker", "Break 10 bricks in a row") },
				{ "PowerPlayer", new Achievement("Power Player", "Collect 5 power-ups") },
				{ "BossSlayer", new Achievement("Boss Slayer", "Defeat your first boss") },
				{ "HighScore1000", new Achievement("Score Master", "Reach 1000 points") },
				{ "SpeedRunner", new Achievement("Speed Runner", "Clear a level in under 30 seconds") }
			};
			
			_statistics = new GameStatistics();
			_comboSystem = new ComboSystem();
			Reset();
		}

		public int Score { get; private set; }
		public int Lives { get; private set; } = 3;
		public int Level { get; private set; } = 1;
		public bool GameOver { get; private set; }
		public GameMode CurrentMode { get; set; } = GameMode.Story;
		public List<int> Leaderboard { get; } = new List<int>();
		public int ShardStormCharge { get; set; }
		public float EnhancedManeuverabilityTimer { get; set; }
		public float VacuumFieldTimer { get; set; }
		public float PaddleChargeTimer { get; set; }
		public Vector3? GravityWellPosition { get; set; }
		public float GravityWellTimer { get; set; }
		public float PaddleEnlargeTimer { get; set; }
		public bool IsPaddleEnlarged { get; set; }
		
		// New additions
		private Dictionary<string, Achievement> _achievements;
		private GameStatistics _statistics;
		private ComboSystem _comboSystem;
		private float _levelTimer;
		private const int MAX_LEADERBOARD_ENTRIES = 10;
		private Queue<string> _achievementNotificationQueue = new Queue<string>();
		private float _achievementNotificationTimer;
		private const float ACHIEVEMENT_NOTIFICATION_DURATION = 3.0f;

		public void Reset()
		{
			Score = 0;
			Lives = 3;
			Level = 1;
			GameOver = false;
			CurrentMode = GameMode.Story;
			
			ShardStormCharge = 0;
			EnhancedManeuverabilityTimer = 0;
			VacuumFieldTimer = 0;
			PaddleChargeTimer = 0;
			GravityWellPosition = null;
			GravityWellTimer = 0;
			PaddleEnlargeTimer = 0;
			IsPaddleEnlarged = false;
			_levelTimer = 0;
			_comboSystem.ResetCombo();
		}

		public void Update(float deltaTime)
		{
			_levelTimer += deltaTime;
			_comboSystem.Update(deltaTime);
		}

		public void AddScore(int points)
		{
			int multiplier = _comboSystem.GetComboMultiplier();
			Score += points * multiplier;
			CheckAchievements();
		}

		public void OnBrickDestroyed()
		{
			_comboSystem.AddHit();
			_statistics.UpdateStats(GameEvent.BrickDestroyed);
			
			if (_statistics.TotalBricksDestroyed == 1)
				UnlockAchievement("FirstBlood");
				
			if (_comboSystem.CurrentCombo == 10)
				UnlockAchievement("ChainBreaker");
		}

		public void OnPowerUpCollected(PowerUpType type)
		{
			_statistics.UpdateStats(GameEvent.PowerUpCollected, type);
			
			if (_statistics.TotalPowerUpsCollected == 5)
				UnlockAchievement("PowerPlayer");
		}

		public void OnBossDefeated()
		{
			_statistics.UpdateStats(GameEvent.BossDefeated);
			UnlockAchievement("BossSlayer");
		}

		public void LoseLife()
		{
			Lives--;
			_comboSystem.ResetCombo();
			
			if (Lives <= 0)
			{
				GameOver = true;
				UpdateLeaderboard(Score);
			}
		}

		private void UpdateLeaderboard(int score)
		{
			Leaderboard.Add(score);
			Leaderboard.Sort((a, b) => b.CompareTo(a));
			
			if (Leaderboard.Count > MAX_LEADERBOARD_ENTRIES)
				Leaderboard.RemoveAt(MAX_LEADERBOARD_ENTRIES);
		}

		private void UnlockAchievement(string achievementId)
		{
			if (_achievements.ContainsKey(achievementId) && !_achievements[achievementId].IsUnlocked)
			{
				_achievements[achievementId].Unlock();
				ShowAchievementNotification(_achievements[achievementId].Name);
			}
		}

		public void CheckAchievements(int brickCount = 0, int currentScore = 0)
		{
			if (currentScore >= 1000)
				UnlockAchievement("HighScore1000");
				
			if (_levelTimer <= 30 && Level > 1)
				UnlockAchievement("SpeedRunner");
		}

		public List<Achievement> GetUnlockedAchievements()
		{
			return _achievements.Values.Where(a => a.IsUnlocked).ToList();
		}

		public GameStatistics GetStatistics()
		{
			return _statistics;
		}

		public int GetCurrentCombo()
		{
			return _comboSystem.CurrentCombo;
		}

		public void ShowAchievementNotification(string achievementName)
		{
			lock (_achievementLock)
			{
				_achievementNotificationQueue ??= new Queue<string>();
				_achievementNotificationQueue.Enqueue($"Achievement Unlocked: {achievementName}");
				_achievementNotificationTimer = ACHIEVEMENT_NOTIFICATION_DURATION;
			}
		}

		public void GainLife()
		{
			Lives++;
		}

		public void NextLevel()
		{
			Level++;
			_levelTimer = 0;
		}
	}



	public class InputHandler
	{
		public bool HandleExit(KeyboardState keyboardState)
		{
			return keyboardState.IsKeyDown(Keys.Escape);
		}

		public bool HandleRestart(KeyboardState keyboardState)
		{
			return keyboardState.IsKeyDown(Keys.Space);
		}

		public GameMode HandleModeSelection(KeyboardState keyboardState, GameMode currentMode)
		{
			if (keyboardState.IsKeyDown(Keys.D1)) return GameMode.Story;
			if (keyboardState.IsKeyDown(Keys.D2)) return GameMode.Endless;
			if (keyboardState.IsKeyDown(Keys.D3)) return GameMode.BossRush;
			if (keyboardState.IsKeyDown(Keys.D4)) return GameMode.TimeAttack;
			if (keyboardState.IsKeyDown(Keys.D5)) return GameMode.CoOp;
			return currentMode;
		}
	}

	public class Paddle
	{
		public RigidBody PhysicsBody { get; }
		public Texture2D Texture { get; set; }
		public float Width => Texture?.Width ?? 100f;
		public float Height => Texture?.Height ?? 20f;
		public Vector3 Position => PhysicsBody?.Position ?? new Vector3();

		public Paddle()
		{
			PhysicsBody = new RigidBody
			{
				Position = new Vector3(400, 550, 0),
				TransformMatrix = new Matrix4(new float[16]),
				Orientation = new Quaternion(0, 0, 0, 1),
				InverseInertiaTensor = new Matrix3(),
				Velocity = new Vector3(0, 0, 0),
				acceleration = new Vector3(0, 0, 0),
				forceAccum = new Vector3(0, 0, 0),
				torqueAccum = new Vector3(0, 0, 0),
				linearDamping = 0.95f
			};
			PhysicsBody.TransformMatrix.SetOrientationAndPos(PhysicsBody.Orientation, PhysicsBody.Position);
		}

		public void Draw(SpriteBatch spriteBatch)
		{
			if (Texture != null)
				spriteBatch.Draw(Texture, new Vector2(Position.X - Width / 2, Position.Y - Height / 2), Color.White);
		}
	}

	public class Ball
	{
		public RigidBody PhysicsBody { get; }
		public Texture2D Texture { get; set; }
		public float Radius => Texture?.Width / 2f ?? 10f;
		public Vector3 Position => PhysicsBody?.Position ?? new Vector3();

		public Ball()
		{
			PhysicsBody = new RigidBody
			{
				Position = new Vector3(400, 500, 0),
				TransformMatrix = new Matrix4(new float[16]),
				Orientation = new Quaternion(0, 0, 0, 1),
				InverseInertiaTensor = new Matrix3(),
				Velocity = new Vector3(200, -300, 0),
				acceleration = new Vector3(0, 0, 0),
				forceAccum = new Vector3(0, 0, 0),
				torqueAccum = new Vector3(0, 0, 0),
				linearDamping = 0.99f
			};
			PhysicsBody?.TransformMatrix?.SetOrientationAndPos(PhysicsBody.Orientation, PhysicsBody.Position);
		}

		public void Draw(SpriteBatch spriteBatch)
		{
			if (Texture != null)
				spriteBatch.Draw(Texture, new Vector2(Position.X - Radius, Position.Y - Radius), Color.White);
		}
	}

	public class Brick
	{
		public RigidBody PhysicsBody { get; }
		public Texture2D Texture { get; set; }
		public float Width => Texture?.Width ?? 80f;
		public float Height => Texture?.Height ?? 30f;
		public Vector3 Position => PhysicsBody?.Position ?? new Vector3();
		public int Health { get; set; }
		public Color BaseColor { get; }

		public Brick(Vector3 position, Texture2D texture, int health, Color baseColor)
		{
			PhysicsBody = new RigidBody
			{
				Position = position,
				TransformMatrix = new Matrix4(new float[16]),
				Orientation = new Quaternion(0, 0, 0, 1),
				InverseInertiaTensor = new Matrix3(),
				Velocity = new Vector3(0, 0, 0),
				acceleration = new Vector3(0, 0, 0),
				forceAccum = new Vector3(0, 0, 0),
				torqueAccum = new Vector3(0, 0, 0),
				linearDamping = 0.95f
			};
			PhysicsBody.TransformMatrix.SetOrientationAndPos(PhysicsBody.Orientation, PhysicsBody.Position);
			Texture = texture;
			Health = health;
			BaseColor = baseColor;
		}

		public void Draw(SpriteBatch spriteBatch)
		{
			if (Texture != null)
				spriteBatch.Draw(Texture, new Vector2(Position.X - Width / 2, Position.Y - Height / 2), Color.White);
		}
	}

	public class Boss
	{
		public RigidBody PhysicsBody { get; }
		public Texture2D Texture { get; }
		public float Health { get; set; }
		public float Width => Texture?.Width ?? 100f;
		public float Height => Texture?.Height ?? 50f;
		public Vector3 Position => PhysicsBody?.Position ?? new Vector3();

		private float _moveSpeed = 100f;
		private float _direction = 1f;

		public Boss(Vector3 position, Texture2D texture)
		{
			PhysicsBody = new RigidBody
			{
				Position = position,
				TransformMatrix = new Matrix4(new float[16]),
				Orientation = new Quaternion(0, 0, 0, 1),
				InverseInertiaTensor = new Matrix3(),
				Velocity = new Vector3(0, 0, 0),
				acceleration = new Vector3(0, 0, 0),
				forceAccum = new Vector3(0, 0, 0),
				torqueAccum = new Vector3(0, 0, 0),
				linearDamping = 0.95f,
				InverseMass = 0f
			};
			PhysicsBody.TransformMatrix.SetOrientationAndPos(PhysicsBody.Orientation, PhysicsBody.Position);
			Texture = texture;
			Health = 100f;
		}

		public void Update(float deltaTime)
		{
			PhysicsBody.Position.X += _moveSpeed * _direction * deltaTime;
			if (PhysicsBody.Position.X < Width / 2 || PhysicsBody.Position.X > BrickBreakerGame.SCREEN_WIDTH - Width / 2)
				_direction *= -1;
			PhysicsBody.TransformMatrix.SetOrientationAndPos(PhysicsBody.Orientation, PhysicsBody.Position);
		}

		public void Draw(SpriteBatch spriteBatch)
		{
			if (Texture != null)
				spriteBatch.Draw(Texture, new Vector2(Position.X - Width / 2, Position.Y - Height / 2), Color.White);
		}
	}

	public enum PowerUpType
	{
		MultiBall,
		ExtraLife,
		EnhancedManeuverability,
		ShardStormCharge,
		VacuumField,
		GravityWell
	}

	public class PowerUp : IDisposable
	{
		public PowerUpType Type { get; }
		public Vector3 Position { get; set; }
		private readonly Texture2D _texture;
		
		public PowerUp(PowerUpType type, Vector3 position, Texture2D texture)
		{
			Type = type;
			Position = position;
			_texture = texture;
		}
		
		public void Draw(SpriteBatch spriteBatch)
		{
			if (_texture != null)
				spriteBatch.Draw(_texture, new Vector2(Position.X - 15, Position.Y - 15), Color.White);
		}
		
		public void Dispose()
		{
			_texture?.Dispose();
		}
	}

	public class Particle : IDisposable
	{
		public Vector3 Position { get; set; }
		public Vector3 Velocity { get; set; }
		public float Life { get; set; }
		private Texture2D _texture;

		public Particle(Vector3 position, Random random, GraphicsDevice graphicsDevice)
		{
			Position = position;
			Velocity = new Vector3((float)(random.NextDouble() - 0.5) * 100, (float)(random.NextDouble() - 0.5) * 100, 0);
			Life = (float)random.NextDouble() * 1f + 0.5f;
			_texture = new Texture2D(graphicsDevice, 5, 5);
			var data = new Color[25];
			for (int i = 0; i < 25; i++) data[i] = Color.Yellow;
			_texture.SetData(data);
		}

		public void Update(float deltaTime)
		{
			Position += Velocity * deltaTime;
			Life -= deltaTime;
		}

		public void Draw(SpriteBatch spriteBatch)
		{
			if (Life > 0)
				spriteBatch.Draw(_texture, new Vector2(Position.X - 2.5f, Position.Y - 2.5f), Color.White * (Life / 1.5f));
		}

		public void Dispose()
		{
			_texture?.Dispose();
		}
	}

	public class Fragment : IDisposable
	{
		public Vector3 Position { get; set; }
		public Vector3 Velocity { get; set; }
		public float Life { get; set; }
		public float Radius => 5f;
		private Texture2D _texture;

		public Fragment(Vector3 position, Random random, GraphicsDevice graphicsDevice)
		{
			Position = position;
			Velocity = new Vector3((float)(random.NextDouble() - 0.5) * 200, (float)(random.NextDouble() - 0.5) * 200, 0);
			Life = 2f;
			_texture = new Texture2D(graphicsDevice, 10, 10);
			var data = new Color[100];
			for (int y = 0; y < 10; y++)
			{
				for (int x = 0; x < 10; x++)
				{
					float dx = x - 5f;
					float dy = y - 5f;
					data[y * 10 + x] = (dx * dx + dy * dy <= 25) ? Color.Gray : Color.Transparent;
				}
			}
			_texture.SetData(data);
		}

		public void Update(float deltaTime)
		{
			Position += Velocity * deltaTime;
			Life -= deltaTime;
		}

		public void Draw(SpriteBatch spriteBatch)
		{
			if (Life > 0)
				spriteBatch.Draw(_texture, new Vector2(Position.X - 5, Position.Y - 5), Color.White * (Life / 2f));
		}

		public void Dispose()
		{
			_texture?.Dispose();
		}
	}

	public enum GameMode
	{
		Story,
		Endless,
		BossRush,
		TimeAttack,
		CoOp
	}




	public enum GameState
	{
		MainMenu,
		Playing,
		GameOver,
		Exit
	}

	public class BrickBreakerGame : Game
	{
		private GraphicsDeviceManager _graphics;
		private SpriteBatch _spriteBatch;
		private ParticleWorld _physicsWorld;
		private Random _random;
		public GameManager _gameManager;
		private PhysicsManager _physicsManager;
		private Renderer _renderer;
		private InputHandler _inputHandler;
		private MainMenu _mainMenu;
		private GameState _currentState;
		private Dictionary<char, bool[,]> _characterMap;
		private Dictionary<char, Texture2D> _characterTextures;

		private Paddle _paddle;
		private List<Ball> _balls;
		private List<Brick> _bricks;
		private List<RigidBody> _walls;
		private List<PowerUp> _powerUps;
		private List<Particle> _particles;
		private List<Fragment> _fragments;
		private Boss _boss;

		private Dictionary<string, Texture2D> _textureCache;
		private float _achievementNotificationTimer;
		private const float ACHIEVEMENT_NOTIFICATION_DURATION = 3.0f;
		private string _currentAchievementDisplay;
		private Queue<string> _achievementNotificationQueue = new Queue<string>();

		public static int SCREEN_WIDTH = 800;
		public static int SCREEN_HEIGHT = 600;

		public BrickBreakerGame()
		{
			_graphics = new GraphicsDeviceManager(this);
			
			Content.RootDirectory = "Content";
			_graphics.PreferredBackBufferWidth = SCREEN_WIDTH;
			_graphics.PreferredBackBufferHeight = SCREEN_HEIGHT;
			IsMouseVisible = true;
			_random = new Random();
			_textureCache = new Dictionary<string, Texture2D>();
			_characterMap = InitializeCharacterMap();
			_characterTextures = new Dictionary<char, Texture2D>();
			_currentState = GameState.MainMenu;
			_achievementNotificationTimer = 0;
			if (_graphics.GraphicsDevice == null)
			{
				System.Diagnostics.Debug.WriteLine("GraphicsDevice is null in BrickBreakerGame constructor");
			}
		}

		protected override void Initialize()
		{
			_physicsWorld = new ParticleWorld(100);
			_gameManager = new GameManager(this);  // Pass 'this' reference
			_inputHandler = new InputHandler();
			_mainMenu = new MainMenu(this);

			_paddle = new Paddle();
			_balls = new List<Ball> { new Ball() };
			_bricks = new List<Brick>();
			_walls = new List<RigidBody>();
			_powerUps = new List<PowerUp>();
			_particles = new List<Particle>();
			_fragments = new List<Fragment>();

			SetupPhysics();
			CreateScreenBoundaries();
			SetupLevel();

			_currentState = GameState.MainMenu;
			_achievementNotificationTimer = 0;
			base.Initialize();
		}

		protected override void LoadContent()
		{
			if (GraphicsDevice == null)
			{
				System.Diagnostics.Debug.WriteLine("GraphicsDevice is null in LoadContent");
				return;
			}

			if (_spriteBatch == null)
			{
				_spriteBatch = new SpriteBatch(GraphicsDevice);
				System.Diagnostics.Debug.WriteLine("SpriteBatch initialized in LoadContent");
			}

			_physicsManager = new PhysicsManager(_physicsWorld, this);
			_renderer = new Renderer(_spriteBatch, this);

			_paddle.Texture = CreatePaddleTexture(GraphicsDevice, 100, 20);
			var ballTexture = CreateBallTexture(GraphicsDevice, 20);
			foreach (var ball in _balls)
				ball.Texture = ballTexture;

			// Initialize character textures
			foreach (char c in _characterMap.Keys)
			{
				_characterTextures[c] = CreateCharacterTexture(GraphicsDevice, c);
			}
		}

		protected override void Update(GameTime gameTime)
		{
			if (gameTime == null) return;
			
			float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
			var keyboardState = Keyboard.GetState();
			var mouseState = Mouse.GetState();

			try
			{
				switch (_currentState)
				{
					case GameState.MainMenu:
						GameState menuState = _mainMenu?.Update(keyboardState, mouseState) ?? GameState.MainMenu;
						if (menuState == GameState.Playing)
						{
							RestartGame();
							_currentState = GameState.Playing;
						}
						else if (menuState == GameState.Exit)
						{
							Exit();  // This calls the MonoGame Exit method
							return;  // Return immediately after calling Exit
						}
						break;

					case GameState.Playing:
						if (_gameManager?.GameOver ?? false)
						{
							_currentState = GameState.GameOver;
							break;
						}

						_gameManager?.Update(deltaTime);
						UpdateGame(deltaTime, keyboardState);
						UpdateAchievementNotifications(deltaTime);
						break;

					case GameState.GameOver:
						if (keyboardState.IsKeyDown(Keys.Space))
						{
							_currentState = GameState.MainMenu;
						}
						break;
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error in Update: {ex.Message}");
			}

			base.Update(gameTime);
		}

		protected override void Draw(GameTime gameTime)
		{
			GraphicsDevice.Clear(_currentState == GameState.GameOver ? Color.DarkRed : Color.Black);

			_spriteBatch.Begin();

			switch (_currentState)
			{
				case GameState.MainMenu:
					_mainMenu.Draw(_spriteBatch);
					break;

				case GameState.Playing:
					DrawGame();
					DrawHUD();
					DrawAchievementNotification();
					break;

				case GameState.GameOver:
					_renderer.DrawGameOver();
					break;
			}

			_spriteBatch.End();

			base.Draw(gameTime);
		}

		private void DrawHUD()
		{
			// Draw score with bright color and larger scale
			DrawText(_spriteBatch, $"SCORE: {_gameManager.Score}", 
				new Vector2(15, 15), Color.Yellow, 1.2f);
			
			// Draw lives count
			DrawText(_spriteBatch, $"LIVES: {_gameManager.Lives}", 
				new Vector2(15, 40), Color.LightGreen, 1.2f);
			
			// Draw level with different color
			DrawText(_spriteBatch, $"LEVEL: {_gameManager.Level}", 
				new Vector2(15, 65), Color.LightBlue, 1.2f);

			// Draw combo with enhanced visibility
			int combo = _gameManager.GetCurrentCombo();
			if (combo > 1)
			{
				DrawText(_spriteBatch, $"COMBO: x{combo}", 
					new Vector2(15, 90), Color.Orange, 1.2f);
			}
		}

		private void DrawAchievementNotification()
		{
			if (_achievementNotificationTimer > 0 && !string.IsNullOrEmpty(_currentAchievementDisplay))
			{
				float alpha = Math.Min(1f, _achievementNotificationTimer / ACHIEVEMENT_NOTIFICATION_DURATION);
				
				// Draw background panel for achievement notification
				Texture2D notificationBg = new Texture2D(GraphicsDevice, 400, 40);
				Color[] bgData = new Color[400 * 40];
				for (int i = 0; i < bgData.Length; i++)
					bgData[i] = new Color(0, 0, 0, (int)(180 * alpha));
				notificationBg.SetData(bgData);
				
				Vector2 notificationPos = new Vector2(SCREEN_WIDTH / 2 - 200, 100);
				_spriteBatch.Draw(notificationBg, notificationPos, Color.White * alpha);
				
				// Draw achievement text with enhanced visibility
				DrawText(_spriteBatch, _currentAchievementDisplay,
					new Vector2(notificationPos.X + 10, notificationPos.Y + 10),
					Color.Gold * alpha, 1.2f);
			}
		}

		private void UpdateAchievementNotifications(float deltaTime)
		{
			if (_achievementNotificationTimer > 0)
			{
				_achievementNotificationTimer -= deltaTime;
				if (_achievementNotificationTimer <= 0 && _achievementNotificationQueue.Count > 0)
				{
					_currentAchievementDisplay = _achievementNotificationQueue.Dequeue();
					_achievementNotificationTimer = ACHIEVEMENT_NOTIFICATION_DURATION;
				}
			}
		}

		private void DrawGame()
		{
			_renderer.DrawGame(_paddle, _balls, _bricks, _powerUps, _particles, _fragments, _boss, _gameManager.GravityWellPosition);
		}

		public void DrawText(SpriteBatch spriteBatch, string text, Vector2 position, Color color, float scale)
		{
			float x = position.X;
			float charWidth = 6 * scale;
			float charHeight = 8 * scale;

			// Draw outline/shadow effect first
			Vector2[] outlineOffsets = new Vector2[]
			{
				new Vector2(-1, -1), new Vector2(1, -1),
				new Vector2(-1, 1), new Vector2(1, 1),
				new Vector2(0, -1), new Vector2(0, 1),
				new Vector2(-1, 0), new Vector2(1, 0)
			};

			foreach (var offset in outlineOffsets)
			{
				float offsetX = x;
				foreach (char c in text.ToUpper())
				{
					if (_characterTextures.TryGetValue(c, out Texture2D texture))
					{
						spriteBatch.Draw(texture, 
							new Vector2(offsetX + offset.X, position.Y + offset.Y), 
							null, Color.Black, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
						offsetX += charWidth;
					}
					else if (c == ' ')
					{
						offsetX += charWidth;
					}
				}
			}

			// Draw the main text
			foreach (char c in text.ToUpper())
			{
				if (_characterTextures.TryGetValue(c, out Texture2D texture))
				{
					spriteBatch.Draw(texture, new Vector2(x, position.Y), 
						null, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
					x += charWidth;
				}
				else if (c == ' ')
				{
					x += charWidth;
				}
			}
		}

		private Dictionary<char, bool[,]> InitializeCharacterMap()
		{
			var map = new Dictionary<char, bool[,]>();
			// Define 5x7 pixel patterns for digits and letters
			map['0'] = new bool[,] {
				{ true, true, true, true, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, true, true, true, true }
			};
			map['1'] = new bool[,] {
				{ false, false, true, false, false },
				{ false, true, true, false, false },
				{ false, false, true, false, false },
				{ false, false, true, false, false },
				{ false, false, true, false, false },
				{ false, false, true, false, false },
				{ false, true, true, true, false }
			};
			map['2'] = new bool[,] {
				{ true, true, true, true, true },
				{ false, false, false, false, true },
				{ false, false, false, false, true },
				{ true, true, true, true, true },
				{ true, false, false, false, false },
				{ true, false, false, false, false },
				{ true, true, true, true, true }
			};
			map['3'] = new bool[,] {
				{ true, true, true, true, true },
				{ false, false, false, false, true },
				{ false, false, false, false, true },
				{ true, true, true, true, true },
				{ false, false, false, false, true },
				{ false, false, false, false, true },
				{ true, true, true, true, true }
			};
			map['4'] = new bool[,] {
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, true, true, true, true },
				{ false, false, false, false, true },
				{ false, false, false, false, true },
				{ false, false, false, false, true }
			};
			map['5'] = new bool[,] {
				{ true, true, true, true, true },
				{ true, false, false, false, false },
				{ true, false, false, false, false },
				{ true, true, true, true, true },
				{ false, false, false, false, true },
				{ false, false, false, false, true },
				{ true, true, true, true, true }
			};
			map['6'] = new bool[,] {
				{ true, true, true, true, true },
				{ true, false, false, false, false },
				{ true, false, false, false, false },
				{ true, true, true, true, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, true, true, true, true }
			};
			map['7'] = new bool[,] {
				{ true, true, true, true, true },
				{ false, false, false, false, true },
				{ false, false, false, false, true },
				{ false, false, false, false, true },
				{ false, false, false, false, true },
				{ false, false, false, false, true },
				{ false, false, false, false, true }
			};
			map['8'] = new bool[,] {
				{ true, true, true, true, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, true, true, true, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, true, true, true, true }
			};
			map['9'] = new bool[,] {
				{ true, true, true, true, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, true, true, true, true },
				{ false, false, false, false, true },
				{ false, false, false, false, true },
				{ true, true, true, true, true }
			};
			map['S'] = new bool[,] {
				{ true, true, true, true, true },
				{ true, false, false, false, false },
				{ true, false, false, false, false },
				{ true, true, true, true, true },
				{ false, false, false, false, true },
				{ false, false, false, false, true },
				{ true, true, true, true, true }
			};
			map['C'] = new bool[,] {
				{ true, true, true, true, true },
				{ true, false, false, false, false },
				{ true, false, false, false, false },
				{ true, false, false, false, false },
				{ true, false, false, false, false },
				{ true, false, false, false, false },
				{ true, true, true, true, true }
			};
			map['O'] = new bool[,] {
				{ true, true, true, true, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, true, true, true, true }
			};
			map['R'] = new bool[,] {
				{ true, true, true, true, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, true, true, true, true },
				{ true, false, false, false, false },
				{ true, false, false, false, false },
				{ true, false, false, false, false }
			};
			map['E'] = new bool[,] {
				{ true, true, true, true, true },
				{ true, false, false, false, false },
				{ true, false, false, false, false },
				{ true, true, true, true, true },
				{ true, false, false, false, false },
				{ true, false, false, false, false },
				{ true, true, true, true, true }
			};
			map['L'] = new bool[,] {
				{ true, false, false, false, false },
				{ true, false, false, false, false },
				{ true, false, false, false, false },
				{ true, false, false, false, false },
				{ true, false, false, false, false },
				{ true, false, false, false, false },
				{ true, true, true, true, true }
			};
			map['I'] = new bool[,] {
				{ true, true, true, true, true },
				{ false, false, true, false, false },
				{ false, false, true, false, false },
				{ false, false, true, false, false },
				{ false, false, true, false, false },
				{ false, false, true, false, false },
				{ true, true, true, true, true }
			};
			map['V'] = new bool[,] {
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ false, true, false, true, false },
				{ false, false, true, false, false }
			};
			map['A'] = new bool[,] {
				{ false, false, true, false, false },
				{ false, true, false, true, false },
				{ true, false, false, false, true },
				{ true, true, true, true, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true }
			};
			map['G'] = new bool[,] {
				{ true, true, true, true, true },
				{ true, false, false, false, false },
				{ true, false, false, false, false },
				{ true, false, true, true, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, true, true, true, true }
			};
			map['M'] = new bool[,] {
				{ true, false, false, false, true },
				{ true, true, false, true, true },
				{ true, false, true, false, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true }
			};
			map['P'] = new bool[,] {
				{ true, true, true, true, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, true, true, true, true },
				{ true, false, false, false, false },
				{ true, false, false, false, false },
				{ true, false, false, false, false }
			};
			map['T'] = new bool[,] {
				{ true, true, true, true, true },
				{ false, false, true, false, false },
				{ false, false, true, false, false },
				{ false, false, true, false, false },
				{ false, false, true, false, false },
				{ false, false, true, false, false },
				{ false, false, true, false, false }
			};
			map[':'] = new bool[,] {
				{ false, false, false, false, false },
				{ false, false, true, false, false },
				{ false, false, true, false, false },
				{ false, false, false, false, false },
				{ false, false, true, false, false },
				{ false, false, true, false, false },
				{ false, false, false, false, false }
			};

			// Add missing characters
			map['Y'] = new bool[,] {
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ false, true, false, true, false },
				{ false, false, true, false, false },
				{ false, false, true, false, false },
				{ false, false, true, false, false },
				{ false, false, true, false, false }
			};

			map['H'] = new bool[,] {
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, true, true, true, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true }
			};

			map['D'] = new bool[,] {
				{ true, true, true, true, false },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, true, true, true, false }
			};

			map['W'] = new bool[,] {
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, false, true, false, true },
				{ true, false, true, false, true },
				{ true, true, false, true, true },
				{ true, false, false, false, true }
			};

			map['X'] = new bool[,] {
				{ true, false, false, false, true },
				{ false, true, false, true, false },
				{ false, false, true, false, false },
				{ false, false, true, false, false },
				{ false, true, false, true, false },
				{ true, false, false, false, true },
				{ true, false, false, false, true }
			};

			map['K'] = new bool[,] {
				{ true, false, false, false, true },
				{ true, false, false, true, false },
				{ true, false, true, false, false },
				{ true, true, false, false, false },
				{ true, false, true, false, false },
				{ true, false, false, true, false },
				{ true, false, false, false, true }
			};

			map['N'] = new bool[,] {
				{ true, false, false, false, true },
				{ true, true, false, false, true },
				{ true, false, true, false, true },
				{ true, false, false, true, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true }
			};

			map['U'] = new bool[,] {
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, true, true, true, true }
			};

			map['F'] = new bool[,] {
				{ true, true, true, true, true },
				{ true, false, false, false, false },
				{ true, false, false, false, false },
				{ true, true, true, true, false },
				{ true, false, false, false, false },
				{ true, false, false, false, false },
				{ true, false, false, false, false }
			};

			map['B'] = new bool[,] {
				{ true, true, true, true, false },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, true, true, true, false },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, true, true, true, false }
			};

			map['J'] = new bool[,] {
				{ false, false, false, false, true },
				{ false, false, false, false, true },
				{ false, false, false, false, true },
				{ false, false, false, false, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, true, true, true, true }
			};

			map['Q'] = new bool[,] {
				{ true, true, true, true, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, false, true, false, true },
				{ true, false, false, true, true },
				{ true, true, true, true, true }
			};

			map['Z'] = new bool[,] {
				{ true, true, true, true, true },
				{ false, false, false, false, true },
				{ false, false, false, true, false },
				{ false, false, true, false, false },
				{ false, true, false, false, false },
				{ true, false, false, false, false },
				{ true, true, true, true, true }
			};

			// Add missing characters
			map['R'] = new bool[,] {
				{ true, true, true, true, false },
				{ true, false, false, false, true },
				{ true, false, false, false, true },
				{ true, true, true, true, false },
				{ true, false, true, false, false },
				{ true, false, false, true, false },
				{ true, false, false, false, true }
			};

			return map;
		}

		private Texture2D CreateCharacterTexture(GraphicsDevice device, char c)
		{
			if (!_characterMap.TryGetValue(c, out bool[,] pattern))
				return null;

			int width = 5;
			int height = 7;
			Texture2D texture = new Texture2D(device, width, height);
			Color[] data = new Color[width * height];

			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					data[y * width + x] = pattern[y, x] ? Color.White : Color.Transparent;
				}
			}

			texture.SetData(data);
			return texture;
		}

		private void SetupPhysics()
		{
			_paddle.PhysicsBody.InverseMass = 0f;
			_paddle.PhysicsBody.linearDamping = 0.95f;
			_paddle.PhysicsBody.SetCanSleep(false);
			_paddle.PhysicsBody.SetInertiaTensor(CreateBoxInertiaTensor(_paddle.Width, _paddle.Height, 1f, 1000f));
			_physicsWorld.AddBody(_paddle.PhysicsBody);

			foreach (var ball in _balls)
			{
				ball.PhysicsBody.InverseMass = 1f;
				ball.PhysicsBody.linearDamping = 0.99f;
				ball.PhysicsBody.Velocity = new Vector3(200, -300, 0);
				ball.PhysicsBody.SetCanSleep(false);
				ball.PhysicsBody.SetInertiaTensor(CreateSphereInertiaTensor(ball.Radius, 1f));
				ball.PhysicsBody.isAwake = true;
				_physicsWorld.AddBody(ball.PhysicsBody);
			}
		}

		public void SetupLevel()
		{
			_bricks.Clear();
			_boss = null;

			switch (_gameManager.CurrentMode)
			{
				case GameMode.Story:
					SetupLevelBricks(10, Math.Min(4 + _gameManager.Level, 10));
					if (_gameManager.Level % 3 == 0)
					{
						_boss = new Boss(new Vector3(SCREEN_WIDTH / 2, 100, 0), CreateBossTexture(GraphicsDevice));
						_physicsWorld.AddBody(_boss.PhysicsBody);
					}
					break;
				case GameMode.Endless:
					SetupLevelBricks(10, 2);
					break;
				case GameMode.BossRush:
					_boss = new Boss(new Vector3(SCREEN_WIDTH / 2, 100, 0), CreateBossTexture(GraphicsDevice));
					_physicsWorld.AddBody(_boss.PhysicsBody);
					break;
				case GameMode.TimeAttack:
				case GameMode.CoOp:
					SetupLevelBricks(10, 4);
					break;
			}
		}

		private void SetupLevelBricks(int bricksPerRow, int brickRows)
		{
			float baseBrickWidth = SCREEN_WIDTH / bricksPerRow;
			float baseBrickHeight = 30;

			for (int y = 0; y < brickRows; y++)
			{
				for (int x = 0; x < bricksPerRow; x++)
				{
					float widthVariation = (float)(_random.NextDouble() * 0.4 + 0.8);
					float heightVariation = (float)(_random.NextDouble() * 0.4 + 0.8);
					int width = (int)(baseBrickWidth * widthVariation);
					int height = (int)(baseBrickHeight * heightVariation);
					Color color = new Color(_random.Next(50, 256), _random.Next(50, 256), _random.Next(50, 256));
					int health = _random.Next(1, 4);
					var brick = new Brick(
						new Vector3(x * baseBrickWidth + width / 2, y * baseBrickHeight + height / 2, 0),
						CreateBrickTexture(GraphicsDevice, width, height, color, health),
						health,
						color
					);
					brick.PhysicsBody.SetInertiaTensor(CreateBoxInertiaTensor(brick.Width, brick.Height, 1f, 1000f));
					brick.PhysicsBody.InverseMass = 0f;
					_bricks.Add(brick);
					_physicsWorld.AddBody(brick.PhysicsBody);
				}
			}
		}

		private Matrix3 CreateBoxInertiaTensor(float width, float height, float depth, float mass)
		{
			float invMass = mass > 0 ? 1f / mass : 0f;
			float[] data = new float[9];
			data[0] = (1f / 12f) * mass * (height * height + depth * depth);
			data[4] = (1f / 12f) * mass * (width * width + depth * depth);
			data[8] = (1f / 12f) * mass * (width * width + height * height);
			return new Matrix3(data).Inverse();
		}

		public Matrix3 CreateSphereInertiaTensor(float radius, float mass)
		{
			float inertia = (2f / 5f) * mass * radius * radius;
			float[] data = new float[9] { inertia, 0, 0, 0, inertia, 0, 0, 0, inertia };
			return new Matrix3(data).Inverse();
		}

		private void CreateScreenBoundaries()
		{
			float wallMass = 1000f;
			Matrix3 wallInertia = CreateBoxInertiaTensor(SCREEN_WIDTH, SCREEN_HEIGHT, 1f, wallMass);

			var walls = new[]
			{
				new RigidBody { Position = new Vector3(-10, SCREEN_HEIGHT / 2f, 0) }, // Left
                new RigidBody { Position = new Vector3(SCREEN_WIDTH + 10, SCREEN_HEIGHT / 2f, 0) }, // Right
                new RigidBody { Position = new Vector3(SCREEN_WIDTH / 2f, -10, 0) }, // Top
                new RigidBody { Position = new Vector3(SCREEN_WIDTH / 2f, SCREEN_HEIGHT + 10, 0) } // Bottom
            };

			foreach (var wall in walls)
			{
				wall.InverseMass = 0f;
				wall.TransformMatrix = new Matrix4(new float[16]);
				wall.Orientation = new Quaternion(0, 0, 0, 1);
				wall.InverseInertiaTensor = wallInertia;
				wall.Velocity = new Vector3(0, 0, 0);
				wall.acceleration = new Vector3(0, 0, 0);
				wall.linearDamping = 0.95f;
				wall.forceAccum = new Vector3(0, 0, 0);
				wall.torqueAccum = new Vector3(0, 0, 0);
				wall.TransformMatrix.SetOrientationAndPos(wall.Orientation, wall.Position);
				_physicsWorld.AddBody(wall);
				_walls.Add(wall);
			}
		}

		private void RestartGame()
		{
			_gameManager.Reset();
			_balls.Clear();
			_powerUps.Clear();
			_fragments.Clear();
			_paddle.Texture = CreatePaddleTexture(GraphicsDevice, 100, 20);
			_gameManager.IsPaddleEnlarged = false;
			_gameManager.PaddleEnlargeTimer = 0f;
			_gameManager.ShardStormCharge = 0;
			_gameManager.VacuumFieldTimer = 0f;
			_gameManager.PaddleChargeTimer = 0f;
			_gameManager.GravityWellPosition = null;
			_gameManager.GravityWellTimer = 0f;

			Ball newBall = new Ball();
			newBall.Texture = CreateBallTexture(GraphicsDevice, 20);
			newBall.PhysicsBody.InverseMass = 1f;
			newBall.PhysicsBody.linearDamping = 0.99f;
			newBall.PhysicsBody.Velocity = new Vector3(200, -300, 0);
			newBall.PhysicsBody.SetInertiaTensor(CreateSphereInertiaTensor(newBall.Radius, 1f));
			newBall.PhysicsBody.isAwake = true;
			_balls.Add(newBall);
			_physicsWorld.AddBody(newBall.PhysicsBody);
			SetupLevel();
		}

		protected override void UnloadContent()
		{
			_mainMenu?.Dispose();
			_renderer?.Dispose();
			
			foreach (var particle in _particles)
				particle?.Dispose();
			_particles.Clear();
			
			foreach (var fragment in _fragments)
				fragment?.Dispose();
			_fragments.Clear();
			
			foreach (var texture in _textureCache.Values)
				texture?.Dispose();
			_textureCache.Clear();
			
			foreach (var texture in _characterTextures.Values)
				texture?.Dispose();
			_characterTextures.Clear();
			
			base.UnloadContent();
		}

		private Texture2D CreatePaddleTexture(GraphicsDevice device, int width, int height)
		{
			string key = $"paddle_{width}_{height}_{_gameManager.PaddleChargeTimer}";
			if (_textureCache.TryGetValue(key, out Texture2D texture))
				return texture;

			texture = new Texture2D(device, width, height);
			var data = new Color[width * height];
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					bool isBorder = x < 2 || x >= width - 2 || y < 2 || y >= height - 2;
					data[y * width + x] = isBorder ? Color.Black : (_gameManager.PaddleChargeTimer > 0 ? Color.Yellow : Color.White);
				}
			}
			texture.SetData(data);
			_textureCache[key] = texture;
			return texture;
		}

		public Texture2D CreateBallTexture(GraphicsDevice device, int size)
		{
			string key = $"ball_{size}";
			if (_textureCache.TryGetValue(key, out Texture2D texture))
				return texture;

			texture = new Texture2D(device, size, size);
			var data = new Color[size * size];
			float radius = size / 2f;
			float radiusSq = radius * radius;

			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float dx = x - radius + 0.5f;
					float dy = y - radius + 0.5f;
					float distSq = dx * dx + dy * dy;
					data[y * size + x] = distSq <= (radius - 1) * (radius - 1) ? Color.White :
										 distSq <= radiusSq ? Color.Black : Color.Transparent;
				}
			}
			texture.SetData(data);
			_textureCache[key] = texture;
			return texture;
		}

		public Texture2D CreateBrickTexture(GraphicsDevice device, int width, int height, Color color, int health)
		{
			string key = $"brick_{width}_{height}_{color.R}_{color.G}_{color.B}_{health}";
			if (_textureCache.TryGetValue(key, out Texture2D texture))
				return texture;

			texture = new Texture2D(device, width, height);
			var data = new Color[width * height];
			Color baseColor = new Color(color.R * health / 3f, color.G * health / 3f, color.B * health / 3f);
			Color borderColor = new Color(baseColor.R / 2, baseColor.G / 2, baseColor.B / 2);

			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					bool isBorder = x < 2 || x >= width - 2 || y < 2 || y >= height - 2;
					data[y * width + x] = isBorder ? borderColor : baseColor;
				}
			}
			texture.SetData(data);
			_textureCache[key] = texture;
			return texture;
		}

		public Texture2D CreatePowerUpTexture(GraphicsDevice device, PowerUpType type)
		{
			string key = $"powerup_{type}";
			if (_textureCache.TryGetValue(key, out Texture2D texture))
				return texture;

			texture = new Texture2D(device, 30, 30);
			var data = new Color[30 * 30];
			Color fillColor = type switch
			{
				PowerUpType.MultiBall => Color.Red,
				PowerUpType.ExtraLife => Color.Green,
				PowerUpType.EnhancedManeuverability => Color.Yellow,
				PowerUpType.ShardStormCharge => Color.Purple,
				PowerUpType.VacuumField => Color.Cyan,
				PowerUpType.GravityWell => Color.Magenta,
				_ => Color.White
			};

			for (int y = 0; y < 30; y++)
			{
				for (int x = 0; x < 30; x++)
				{
					bool isBorder = x < 2 || x >= 28 || y < 2 || y >= 28;
					data[y * 30 + x] = isBorder ? Color.Black : fillColor;
				}
			}
			texture.SetData(data);
			_textureCache[key] = texture;
			return texture;
		}

		private Texture2D CreateBossTexture(GraphicsDevice device)
		{
			string key = "boss";
			if (_textureCache.TryGetValue(key, out Texture2D texture))
				return texture;

			texture = new Texture2D(device, 100, 50);
			var data = new Color[100 * 50];
			for (int y = 0; y < 50; y++)
			{
				for (int x = 0; x < 100; x++)
				{
					bool isBorder = x < 2 || x >= 98 || y < 2 || y >= 48;
					data[y * 100 + x] = isBorder ? Color.Black : Color.Orange;
				}
			}
			texture.SetData(data);
			_textureCache[key] = texture;
			return texture;
		}

		private Texture2D CreateBackgroundTexture(GraphicsDevice device)
		{
			if (device == null) return null;
			Texture2D texture = new Texture2D(device, BrickBreakerGame.SCREEN_WIDTH, BrickBreakerGame.SCREEN_HEIGHT);
				Color[] data = new Color[BrickBreakerGame.SCREEN_WIDTH * BrickBreakerGame.SCREEN_HEIGHT];
				for (int i = 0; i < data.Length; i++)
				{
					data[i] = Color.DarkBlue * 0.5f;
				}
				texture.SetData(data);
				return texture;
		}

		private Texture2D CreateButtonTexture(GraphicsDevice device)
		{
			if (device == null) return null;
			Texture2D texture = new Texture2D(device, 200, 50);
			Color[] data = new Color[200 * 50];
			for (int y = 0; y < 50; y++)
			{
				for (int x = 0; x < 200; x++)
				{
					bool isBorder = x < 2 || x >= 198 || y < 2 || y >= 48;
					bool isGradient = !isBorder;
					
					if (isBorder)
					{
						data[y * 200 + x] = Color.White;
					}
					else if (isGradient)
					{
						float gradientFactor = (y / 50f);
						data[y * 200 + x] = new Color(
							0.3f + gradientFactor * 0.2f,
							0.3f + gradientFactor * 0.2f,
							0.3f + gradientFactor * 0.2f,
							1f
						);
					}
				}
			}
			texture.SetData(data);
			return texture;
		}

		private void UpdateGame(float deltaTime, KeyboardState keyboardState)
		{
			if (_gameManager.GameOver) return;

			_gameManager.CurrentMode = _inputHandler.HandleModeSelection(keyboardState, _gameManager.CurrentMode);
			
			int localShardStormCharge = _gameManager.ShardStormCharge;
			float localPaddleChargeTimer = _gameManager.PaddleChargeTimer;
			float localVacuumFieldTimer = _gameManager.VacuumFieldTimer;
			float localGravityWellTimer = _gameManager.GravityWellTimer;
			Vector3? localGravityWellPosition = _gameManager.GravityWellPosition;

			_physicsManager.UpdateMechanics(_balls, _paddle, keyboardState, deltaTime,
				ref localShardStormCharge, ref localPaddleChargeTimer, ref localVacuumFieldTimer,
				ref localGravityWellTimer, ref localGravityWellPosition, _bricks, _particles, _fragments, _random);

			_gameManager.ShardStormCharge = localShardStormCharge;
			_gameManager.PaddleChargeTimer = localPaddleChargeTimer;
			_gameManager.VacuumFieldTimer = localVacuumFieldTimer;
			_gameManager.GravityWellTimer = localGravityWellTimer;
			_gameManager.GravityWellPosition = localGravityWellPosition;

			_physicsManager.UpdatePowerUps(_powerUps, _paddle, deltaTime, SCREEN_HEIGHT,
				_gameManager, _balls, _physicsWorld, _random, CreateBallTexture(GraphicsDevice, 20));

			float localPaddleEnlargeTimer = _gameManager.PaddleEnlargeTimer;
			bool localIsPaddleEnlarged = _gameManager.IsPaddleEnlarged;
			float localEnhancedManeuverabilityTimer = _gameManager.EnhancedManeuverabilityTimer;

			_physicsManager.UpdateEffects(_particles, _fragments, _boss, deltaTime, SCREEN_HEIGHT,
				ref localPaddleEnlargeTimer, ref localIsPaddleEnlarged, ref _paddle,
				CreatePaddleTexture(GraphicsDevice, 100, 20), ref localEnhancedManeuverabilityTimer,
				_balls, ref localVacuumFieldTimer, ref localGravityWellTimer, ref localGravityWellPosition);

			_gameManager.PaddleEnlargeTimer = localPaddleEnlargeTimer;
			_gameManager.IsPaddleEnlarged = localIsPaddleEnlarged;
			_gameManager.EnhancedManeuverabilityTimer = localEnhancedManeuverabilityTimer;
			_gameManager.VacuumFieldTimer = localVacuumFieldTimer;
			_gameManager.GravityWellTimer = localGravityWellTimer;
			_gameManager.GravityWellPosition = localGravityWellPosition;

			_physicsManager.UpdatePaddle(_paddle, keyboardState, deltaTime, SCREEN_WIDTH);

			_physicsManager.CheckCollisions(_balls, _bricks, _paddle, _boss, _walls, _fragments,
				_particles, _random, _powerUps, SCREEN_HEIGHT, _gameManager, _physicsWorld,
				CreateBrickTexture, CreatePowerUpTexture, ref _boss);

			if (_gameManager.CurrentMode == GameMode.Endless && _bricks.Count < 5)
			{
				SetupLevelBricks(10, 1);
			}
		}

		private void InitializeCharacterTextures()
		{
			if (GraphicsDevice == null) return;
			foreach (char c in _characterMap.Keys)
			{
				if (!_characterTextures.ContainsKey(c))
				{
					_characterTextures[c] = CreateCharacterTexture(GraphicsDevice, c);
				}
			}
		}
	}

	public class MainMenu : IDisposable
	{
		private Texture2D _glowTexture;
		private BrickBreakerGame _game;
		private Texture2D _background;
		private Texture2D _buttonTexture;
		private Rectangle _startButtonRect;
		private Rectangle _exitButtonRect;
		private Rectangle _highScoresRect;
		private Rectangle _gameModeRect;
		private bool _showHighScores;
		private bool _showGameModes;
		private MouseState _previousMouseState;
		private List<GameMode> _availableModes;
		private Dictionary<GameMode, Rectangle> _modeButtons;
		private Texture2D _titleBackground;
		private Texture2D _controlsPanel;
		private const int TITLE_HEIGHT = 100;
		private Color TITLE_GLOW_COLOR = new Color(255, 215, 0) * 0.5f; // Golden glow

		public MainMenu(BrickBreakerGame game)
		{
			_game = game;
			_background = CreateBackgroundTexture(game.GraphicsDevice);
			_buttonTexture = CreateButtonTexture(game.GraphicsDevice);
			_startButtonRect = new Rectangle(BrickBreakerGame.SCREEN_WIDTH / 2 - 150, 200, 300, 60);
			_highScoresRect = new Rectangle(BrickBreakerGame.SCREEN_WIDTH / 2 - 150, 280, 300, 60);
			_exitButtonRect = new Rectangle(BrickBreakerGame.SCREEN_WIDTH / 2 - 150, 360, 300, 60);
			_gameModeRect = new Rectangle(250, 150, 300, 300);
			_showHighScores = false;
			_showGameModes = false;
			
			_availableModes = new List<GameMode> 
			{ 
				GameMode.Story, 
				GameMode.Endless, 
				GameMode.BossRush, 
				GameMode.TimeAttack, 
				GameMode.CoOp 
			};
			
			_modeButtons = new Dictionary<GameMode, Rectangle>();
			int buttonY = 170;
			foreach (var mode in _availableModes)
			{
				_modeButtons[mode] = new Rectangle(275, buttonY, 250, 40);
				buttonY += 50;
			}

			
			_titleBackground = new Texture2D(game.GraphicsDevice, BrickBreakerGame.SCREEN_WIDTH, TITLE_HEIGHT);
			Color[] titleBgData = new Color[BrickBreakerGame.SCREEN_WIDTH * TITLE_HEIGHT];
			for (int y = 0; y < TITLE_HEIGHT; y++)
			{
				for (int x = 0; x < BrickBreakerGame.SCREEN_WIDTH; x++)
				{
					float alpha = 0.8f - (y / (float)TITLE_HEIGHT) * 0.6f;
					titleBgData[y * BrickBreakerGame.SCREEN_WIDTH + x] = new Color(0, 0, 0, alpha);
				}
			}
			_titleBackground.SetData(titleBgData);

			// Create semi-transparent panel for controls
			_controlsPanel = new Texture2D(game.GraphicsDevice, 300, 200);
			Color[] controlsPanelData = new Color[300 * 200];
			for (int i = 0; i < controlsPanelData.Length; i++)
			{
				controlsPanelData[i] = new Color(0, 0, 0, 0.7f);
			}
			_controlsPanel.SetData(controlsPanelData);

			// Initialize the glow texture
			_glowTexture = new Texture2D(game.GraphicsDevice, 1, 1);
			_glowTexture.SetData(new[] { Color.White });
		}

		public void Draw(SpriteBatch spriteBatch)
		{
			try
			{
				if (spriteBatch == null || _background == null || _buttonTexture == null) return;

				// Draw animated background
				spriteBatch.Draw(_background, Vector2.Zero, Color.White);
				float time = (float)DateTime.Now.TimeOfDay.TotalSeconds;
				for (int i = 0; i < 10; i++)
				{
					float x = (float)(Math.Sin(time + i) * 100 + BrickBreakerGame.SCREEN_WIDTH / 2);
					float y = (float)(Math.Cos(time + i) * 100 + BrickBreakerGame.SCREEN_HEIGHT / 2);
					DrawGlowingCircle(spriteBatch, new Vector2(x, y), 20, new Color(0, 0, 255, 50));
				}

				// Draw title background
				spriteBatch.Draw(_titleBackground, Vector2.Zero, Color.White);

				// Draw title with glow effect
				Vector2 titlePos = new Vector2(BrickBreakerGame.SCREEN_WIDTH / 2 - 200, 30);
				// Draw glow
				_game.DrawText(spriteBatch, "BRICK BREAKER",
					titlePos + new Vector2(2, 2), TITLE_GLOW_COLOR, 2.5f);
				// Draw main title
				_game.DrawText(spriteBatch, "BRICK BREAKER",
					titlePos, Color.Yellow, 2.5f);

				if (_showHighScores)
				{
					DrawHighScores(spriteBatch);
				}
				else if (_showGameModes)
				{
					DrawGameModes(spriteBatch);
				}
				else
				{
					// Draw animated buttons with hover effect
					DrawAnimatedButton(spriteBatch, _startButtonRect, "PLAY GAME", Color.Green);
					DrawAnimatedButton(spriteBatch, _highScoresRect, "HIGH SCORES", Color.Blue);
					DrawAnimatedButton(spriteBatch, _exitButtonRect, "EXIT", Color.Red);

					// Draw controls panel with better styling
					spriteBatch.Draw(_controlsPanel, new Vector2(30, 420), Color.White);
					_game.DrawText(spriteBatch, "CONTROLS", new Vector2(50, 430), Color.Yellow, 1.2f);
					_game.DrawText(spriteBatch, "LEFT/RIGHT - MOVE PADDLE", new Vector2(50, 460), Color.White, 0.8f);
					_game.DrawText(spriteBatch, "SPACE - LAUNCH BALL", new Vector2(50, 485), Color.White, 0.8f);
					_game.DrawText(spriteBatch, "C - CHARGE SHOT", new Vector2(50, 510), Color.White, 0.8f);
					_game.DrawText(spriteBatch, "S - SHARD STORM", new Vector2(50, 535), Color.White, 0.8f);

					// Draw version number
					_game.DrawText(spriteBatch, "V1.0", 
						new Vector2(BrickBreakerGame.SCREEN_WIDTH - 60, BrickBreakerGame.SCREEN_HEIGHT - 30),
						Color.Gray, 0.8f);
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error in MainMenu.Draw: {ex.Message}");
			}
		}

		private void DrawAnimatedButton(SpriteBatch spriteBatch, Rectangle rect, string text, Color baseColor)
		{
			MouseState mouseState = Mouse.GetState();
			bool isHovered = rect.Contains(mouseState.Position);
			float scale = isHovered ? 1.1f : 1.0f;
			Color buttonColor = isHovered ? baseColor * 1.2f : baseColor * 0.8f;

			// Draw button shadow
			Rectangle shadowRect = rect;
			shadowRect.Offset(4, 4);
			spriteBatch.Draw(_buttonTexture, shadowRect, Color.Black * 0.5f);

			// Draw main button
			spriteBatch.Draw(_buttonTexture, rect, buttonColor);

			// Calculate text position for centering
			Vector2 textSize = new Vector2(text.Length * 15 * 1.2f, 20 * 1.2f);
			Vector2 textPos = new Vector2(
				rect.X + (rect.Width - textSize.X) / 2,
				rect.Y + (rect.Height - textSize.Y) / 2
			);

			// Draw text with outline
			_game.DrawText(spriteBatch, text, textPos, Color.White, 1.2f);
		}

		private void DrawGlowingCircle(SpriteBatch spriteBatch, Vector2 position, float radius, Color color)
		{
			if (_glowTexture == null || spriteBatch == null) return;

			float scale = radius * 2;
			spriteBatch.Draw(_glowTexture, position, null, color,
				0f, new Vector2(0.5f), new Vector2(scale), SpriteEffects.None, 0f);
		}

		private void DrawHighScores(SpriteBatch spriteBatch)
		{
			// Draw panel background with gradient
			spriteBatch.Draw(_buttonTexture, _gameModeRect, new Color(0, 0, 50, 200));

			// Draw title with glow
			Vector2 titlePos = new Vector2(300, 170);
			_game.DrawText(spriteBatch, "HIGH SCORES",
				titlePos + new Vector2(2, 2), Color.Blue * 0.5f, 1.8f);
			_game.DrawText(spriteBatch, "HIGH SCORES",
				titlePos, Color.Yellow, 1.8f);

			// Draw scores with alternating background
			int y = 220;
			int rank = 1;
			foreach (var score in _game._gameManager.Leaderboard.Take(5))
			{
				Rectangle scoreRect = new Rectangle(275, y - 5, 250, 30);
				spriteBatch.Draw(_buttonTexture, scoreRect,
					rank % 2 == 0 ? new Color(0, 0, 30, 100) : new Color(0, 0, 50, 100));

				_game.DrawText(spriteBatch, $"{rank}. {score}",
					new Vector2(300, y), Color.White, 1.2f);
				y += 40;
				rank++;
			}

			// Draw return instruction with animation
			float alpha = (float)(0.5 + Math.Sin(DateTime.Now.TimeOfDay.TotalSeconds * 2) * 0.5);
			_game.DrawText(spriteBatch, "PRESS ESC TO RETURN",
				new Vector2(300, 400), Color.White * alpha, 1f);
		}

		private void DrawGameModes(SpriteBatch spriteBatch)
		{
			spriteBatch.Draw(_buttonTexture, _gameModeRect, Color.DarkBlue * 0.8f);
			_game.DrawText(spriteBatch, "SELECT MODE", new Vector2(300, 120), Color.Yellow, 1.5f);
			
			foreach (var mode in _modeButtons)
			{
				spriteBatch.Draw(_buttonTexture, mode.Value, Color.Green * 0.8f);
				_game.DrawText(spriteBatch, mode.Key.ToString(), 
					new Vector2(mode.Value.X + 20, mode.Value.Y + 10), Color.White, 1f);
			}
			
			_game.DrawText(spriteBatch, "Press ESC to return", new Vector2(300, 400), Color.White, 0.8f);
		}

		public GameState Update(KeyboardState keyboardState, MouseState mouseState)
		{
			if (keyboardState.IsKeyDown(Keys.Escape))
			{
				if (_showHighScores || _showGameModes)
				{
					_showHighScores = false;
					_showGameModes = false;
					return GameState.MainMenu;
				}
				return GameState.Exit;  // This will trigger the game to exit
			}

			if (mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released)
			{
				if (_showHighScores || _showGameModes)
				{
					if (_showGameModes)
					{
						foreach (var mode in _modeButtons)
						{
							if (mode.Value.Contains(mouseState.Position))
							{
								_game._gameManager.CurrentMode = mode.Key;
								return GameState.Playing;
							}
						}
					}
				}
				else
				{
					if (_startButtonRect.Contains(mouseState.Position))
					{
						_showGameModes = true;
					}
					else if (_highScoresRect.Contains(mouseState.Position))
					{
						_showHighScores = true;
					}
					else if (_exitButtonRect.Contains(mouseState.Position))
					{
						return GameState.Exit;
					}
				}
			}

			_previousMouseState = mouseState;
			return GameState.MainMenu;
		}

		private Texture2D CreateBackgroundTexture(GraphicsDevice device)
		{
			if (device == null) return null;
			Texture2D texture = new Texture2D(device, BrickBreakerGame.SCREEN_WIDTH, BrickBreakerGame.SCREEN_HEIGHT);
			Color[] data = new Color[BrickBreakerGame.SCREEN_WIDTH * BrickBreakerGame.SCREEN_HEIGHT];
			for (int i = 0; i < data.Length; i++)
			{
				data[i] = Color.DarkBlue * 0.5f;
			}
			texture.SetData(data);
			return texture;
		}

		private Texture2D CreateButtonTexture(GraphicsDevice device)
		{
			if (device == null) return null;
			Texture2D texture = new Texture2D(device, 200, 50);
			Color[] data = new Color[200 * 50];
			for (int y = 0; y < 50; y++)
			{
				for (int x = 0; x < 200; x++)
				{
					bool isBorder = x < 2 || x >= 198 || y < 2 || y >= 48;
					bool isGradient = !isBorder;
					
					if (isBorder)
					{
						data[y * 200 + x] = Color.White;
					}
					else if (isGradient)
					{
						float gradientFactor = (y / 50f);
						data[y * 200 + x] = new Color(
							0.3f + gradientFactor * 0.2f,
							0.3f + gradientFactor * 0.2f,
							0.3f + gradientFactor * 0.2f,
							1f
						);
					}
				}
			}
			texture.SetData(data);
			return texture;
		}

		public void Dispose()
		{
			_glowTexture?.Dispose();
			_background?.Dispose();
			_buttonTexture?.Dispose();
			_titleBackground?.Dispose();
			_controlsPanel?.Dispose();
		}
	}

	public class Renderer : IDisposable
	{
		private readonly SpriteBatch _spriteBatch;
		private readonly BrickBreakerGame _game;
		private Texture2D _gameOverTexture;
		private Texture2D _heartTexture;

		public Renderer(SpriteBatch spriteBatch, BrickBreakerGame game)
		{
			_spriteBatch = spriteBatch;
			_game = game ?? throw new ArgumentNullException(nameof(game));
			if (_game?.GraphicsDevice != null)
			{
				_gameOverTexture = CreateGameOverBackground();
				_heartTexture = CreateHeartTexture();
			}
		}

		private Texture2D CreateGameOverBackground()
		{
			Texture2D texture = new Texture2D(_game.GraphicsDevice, 800, 600);
			Color[] data = new Color[800 * 600];
			Color backgroundColor = new Color(139, 69, 19); // Saddle brown color
			
			for (int i = 0; i < data.Length; i++)
			{
				// Create a gradient effect
				int y = i / 800;
				float gradientFactor = 1.0f - (y / 600f);
				data[i] = backgroundColor * (0.7f + (0.3f * gradientFactor));
			}
			
			texture.SetData(data);
			return texture;
		}

		private Texture2D CreateHeartTexture()
		{
			const int size = 20;
			Texture2D texture = new Texture2D(_game.GraphicsDevice, size, size);
			Color[] data = new Color[size * size];
			
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					// Heart shape algorithm
					float dx = x - size/2;
					float dy = y - size/2;
					if ((dx * dx + dy * dy - 25) * (dx * dx + dy * dy - 25) <= 25*dx*dx)
					{
						data[y * size + x] = Color.Red;
					}
					else
					{
						data[y * size + x] = Color.Transparent;
					}
				}
			}
			
			texture.SetData(data);
			return texture;
		}

		public void DrawGame(Paddle paddle, List<Ball> balls, List<Brick> bricks, List<PowerUp> powerUps,
			List<Particle> particles, List<Fragment> fragments, Boss boss, Vector3? gravityWellPosition)
		{
			if (_spriteBatch == null)
			{
				System.Diagnostics.Debug.WriteLine("SpriteBatch is null in Renderer.DrawGame");
				return;
			}

			if (_game?.GraphicsDevice == null)
			{
				System.Diagnostics.Debug.WriteLine("GraphicsDevice is null in Renderer.DrawGame");
				return;
			}

			try
			{
				if (paddle != null) paddle.Draw(_spriteBatch);
				if (balls != null)
					foreach (var ball in balls)
						ball?.Draw(_spriteBatch);
				if (bricks != null)
					foreach (var brick in bricks)
						brick?.Draw(_spriteBatch);
				if (powerUps != null)
					foreach (var powerUp in powerUps)
						powerUp?.Draw(_spriteBatch);
				if (particles != null)
					foreach (var particle in particles)
						particle?.Draw(_spriteBatch);
				if (fragments != null)
					foreach (var fragment in fragments)
						fragment?.Draw(_spriteBatch);
				boss?.Draw(_spriteBatch);

				if (gravityWellPosition != null)
				{
					Vector3 pos = gravityWellPosition;
					Texture2D gravityWellTexture = _game.CreateBallTexture(_game.GraphicsDevice, 20);
					if (gravityWellTexture != null)
					{
						_spriteBatch.Draw(gravityWellTexture,
							new Vector2(pos.X - 10, pos.Y - 10),
							Color.Purple * 0.5f);
					}
				}

				// Draw semi-transparent background panel for HUD
				Texture2D hudBackground = new Texture2D(_game.GraphicsDevice, 200, 100);
				Color[] bgData = new Color[200 * 100];
				for (int i = 0; i < bgData.Length; i++)
					bgData[i] = new Color(0, 0, 0, 180); // Semi-transparent black
				hudBackground.SetData(bgData);
				_spriteBatch.Draw(hudBackground, new Vector2(5, 5), Color.White);

				// Draw lives as hearts
				for (int i = 0; i < _game._gameManager.Lives; i++)
				{
					_spriteBatch.Draw(_heartTexture, 
						new Vector2(BrickBreakerGame.SCREEN_WIDTH - 30 - (i * 25), 15), 
						Color.White);
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error in Renderer.DrawGame: {ex.Message}, StackTrace: {ex.StackTrace}");
			}
		}

		public void DrawGameOver()
		{
			if (_spriteBatch == null || _gameOverTexture == null)
			{
				System.Diagnostics.Debug.WriteLine($"DrawGameOver skipped: SpriteBatch={(_spriteBatch == null)}, gameOverTexture={(_gameOverTexture == null)}");
				return;
			}

			try
			{
				// Draw the background
				_spriteBatch.Draw(_gameOverTexture, Vector2.Zero, Color.White);

				// Draw "GAME OVER" text
				_game.DrawText(_spriteBatch, "GAME OVER", 
					new Vector2(BrickBreakerGame.SCREEN_WIDTH / 2 - 100, 
					BrickBreakerGame.SCREEN_HEIGHT / 2 - 100), 
					Color.White, 2f);

				// Draw final score
				_game.DrawText(_spriteBatch, 
					$"FINAL SCORE: {_game._gameManager.Score}", 
					new Vector2(BrickBreakerGame.SCREEN_WIDTH / 2 - 80, 
					BrickBreakerGame.SCREEN_HEIGHT / 2), 
					Color.Yellow, 1.5f);

				// Draw instruction to continue
				_game.DrawText(_spriteBatch, 
					"PRESS SPACE TO CONTINUE", 
					new Vector2(BrickBreakerGame.SCREEN_WIDTH / 2 - 120, 
					BrickBreakerGame.SCREEN_HEIGHT / 2 + 100), 
					Color.White * (float)(0.5 + Math.Sin(DateTime.Now.Millisecond * 0.01) * 0.5), 1f);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error in DrawGameOver: {ex.Message}, StackTrace: {ex.StackTrace}");
			}
		}

		public void Dispose()
		{
			_gameOverTexture?.Dispose();
			_heartTexture?.Dispose();
		}
	}

}

public class Achievement
{
	public string Name { get; }
	public string Description { get; }
	public bool IsUnlocked { get; private set; }
	public DateTime UnlockTime { get; private set; }

	public Achievement(string name, string description)
	{
		Name = name;
		Description = description;
		IsUnlocked = false;
	}

	public void Unlock()
	{
		if (!IsUnlocked)
		{
			IsUnlocked = true;
			UnlockTime = DateTime.Now;
		}
	}
}

// Add to GameManager class:
public class GameManager
{
	private readonly BrickBreakerGame _game;
	private readonly object _achievementLock = new object();
	
	public GameManager(BrickBreakerGame game)
	{
		_game = game;
		_achievements = new Dictionary<string, Achievement>
		{
			{ "FirstBlood", new Achievement("First Blood", "Break your first brick") },
			{ "ChainBreaker", new Achievement("Chain Breaker", "Break 10 bricks in a row") },
			{ "PowerPlayer", new Achievement("Power Player", "Collect 5 power-ups") },
			{ "BossSlayer", new Achievement("Boss Slayer", "Defeat your first boss") },
			{ "HighScore1000", new Achievement("Score Master", "Reach 1000 points") },
			{ "SpeedRunner", new Achievement("Speed Runner", "Clear a level in under 30 seconds") }
		};
		
		_statistics = new GameStatistics();
		_comboSystem = new ComboSystem();
		Reset();
	}

	public int Score { get; private set; }
	public int Lives { get; private set; } = 3;
	public int Level { get; private set; } = 1;
	public bool GameOver { get; private set; }
	public GameMode CurrentMode { get; set; } = GameMode.Story;
	public List<int> Leaderboard { get; } = new List<int>();
	public int ShardStormCharge { get; set; }
	public float EnhancedManeuverabilityTimer { get; set; }
	public float VacuumFieldTimer { get; set; }
	public float PaddleChargeTimer { get; set; }
	public Vector3? GravityWellPosition { get; set; }
	public float GravityWellTimer { get; set; }
	public float PaddleEnlargeTimer { get; set; }
	public bool IsPaddleEnlarged { get; set; }
	
	// New additions
	private Dictionary<string, Achievement> _achievements;
	private GameStatistics _statistics;
	private ComboSystem _comboSystem;
	private float _levelTimer;
	private const int MAX_LEADERBOARD_ENTRIES = 10;
	private Queue<string> _achievementNotificationQueue = new Queue<string>();
	private float _achievementNotificationTimer;
	private const float ACHIEVEMENT_NOTIFICATION_DURATION = 3.0f;

	public void Reset()
	{
		Score = 0;
		Lives = 3;
		Level = 1;
		GameOver = false;
		CurrentMode = GameMode.Story;
		
		ShardStormCharge = 0;
		EnhancedManeuverabilityTimer = 0;
		VacuumFieldTimer = 0;
		PaddleChargeTimer = 0;
		GravityWellPosition = null;
		GravityWellTimer = 0;
		PaddleEnlargeTimer = 0;
		IsPaddleEnlarged = false;
		_levelTimer = 0;
		_comboSystem.ResetCombo();
	}

	public void Update(float deltaTime)
	{
		_levelTimer += deltaTime;
		_comboSystem.Update(deltaTime);
	}

	public void AddScore(int points)
	{
		int multiplier = _comboSystem.GetComboMultiplier();
		Score += points * multiplier;
		CheckAchievements();
	}

	public void OnBrickDestroyed()
	{
		_comboSystem.AddHit();
		_statistics.UpdateStats(GameEvent.BrickDestroyed);
		
		if (_statistics.TotalBricksDestroyed == 1)
			UnlockAchievement("FirstBlood");
			
		if (_comboSystem.CurrentCombo == 10)
			UnlockAchievement("ChainBreaker");
	}

	public void OnPowerUpCollected(PowerUpType type)
	{
		_statistics.UpdateStats(GameEvent.PowerUpCollected, type);
		
		if (_statistics.TotalPowerUpsCollected == 5)
			UnlockAchievement("PowerPlayer");
	}

	public void OnBossDefeated()
	{
		_statistics.UpdateStats(GameEvent.BossDefeated);
		UnlockAchievement("BossSlayer");
	}

	public void LoseLife()
	{
		Lives--;
		_comboSystem.ResetCombo();
		
		if (Lives <= 0)
		{
			GameOver = true;
			UpdateLeaderboard(Score);
		}
	}

	private void UpdateLeaderboard(int score)
	{
		Leaderboard.Add(score);
		Leaderboard.Sort((a, b) => b.CompareTo(a));
		
		if (Leaderboard.Count > MAX_LEADERBOARD_ENTRIES)
			Leaderboard.RemoveAt(MAX_LEADERBOARD_ENTRIES);
	}

	private void UnlockAchievement(string achievementId)
	{
		if (_achievements.ContainsKey(achievementId) && !_achievements[achievementId].IsUnlocked)
		{
			_achievements[achievementId].Unlock();
			ShowAchievementNotification(_achievements[achievementId].Name);
		}
	}

	public void CheckAchievements(int brickCount = 0, int currentScore = 0)
	{
		if (currentScore >= 1000)
			UnlockAchievement("HighScore1000");
			
		if (_levelTimer <= 30 && Level > 1)
			UnlockAchievement("SpeedRunner");
	}

	public List<Achievement> GetUnlockedAchievements()
	{
		return _achievements.Values.Where(a => a.IsUnlocked).ToList();
	}

	public GameStatistics GetStatistics()
	{
		return _statistics;
	}

	public int GetCurrentCombo()
	{
		return _comboSystem.CurrentCombo;
	}

	public void ShowAchievementNotification(string achievementName)
	{
		lock (_achievementLock)
		{
			_achievementNotificationQueue ??= new Queue<string>();
			_achievementNotificationQueue.Enqueue($"Achievement Unlocked: {achievementName}");
			_achievementNotificationTimer = ACHIEVEMENT_NOTIFICATION_DURATION;
		}
	}

	public void GainLife()
	{
		Lives++;
	}

	public void NextLevel()
	{
		Level++;
		_levelTimer = 0;
	}
}

public class GameStatistics
{
    public int TotalBricksDestroyed { get; private set; }
    public int TotalPowerUpsCollected { get; private set; }
    public int HighestCombo { get; private set; }
    public TimeSpan LongestPlayTime { get; private set; }
    public int BossesDefeated { get; private set; }
    public Dictionary<PowerUpType, int> PowerUpStats { get; }

    public GameStatistics()
    {
        PowerUpStats = new Dictionary<PowerUpType, int>();
        foreach (PowerUpType type in Enum.GetValues(typeof(PowerUpType)))
        {
            PowerUpStats[type] = 0;
        }
    }

    public void UpdateStats(GameEvent gameEvent, object data = null)
    {
        switch (gameEvent)
        {
            case GameEvent.BrickDestroyed:
                TotalBricksDestroyed++;
                break;
            case GameEvent.PowerUpCollected:
                TotalPowerUpsCollected++;
                if (data is PowerUpType powerUpType)
                    PowerUpStats[powerUpType]++;
                break;
            case GameEvent.ComboAchieved:
                if (data is int combo)
                    HighestCombo = Math.Max(HighestCombo, combo);
                break;
            case GameEvent.BossDefeated:
                BossesDefeated++;
                break;
        }
    }
}

public enum GameEvent
{
    BrickDestroyed,
    PowerUpCollected,
    ComboAchieved,
    BossDefeated
}

public class ComboSystem
{
    private int _currentCombo;
    private float _comboTimer;
    private const float COMBO_TIMEOUT = 2.0f;
    
    public int CurrentCombo => _currentCombo;
    
    public void Update(float deltaTime)
    {
        if (_currentCombo > 0)
        {
            _comboTimer -= deltaTime;
            if (_comboTimer <= 0)
            {
                ResetCombo();
            }
        }
    }
    
    public void AddHit()
    {
        _currentCombo++;
        _comboTimer = COMBO_TIMEOUT;
    }
    
    public void ResetCombo()
    {
        _currentCombo = 0;
        _comboTimer = 0;
    }
    
    public int GetComboMultiplier()
    {
        return Math.Max(1, _currentCombo / 5);
    }
}