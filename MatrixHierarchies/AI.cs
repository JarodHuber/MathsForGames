﻿using Raylib_cs;
using System;
namespace MatrixHierarchies
{
    class AI : Tank
    {
        EnemyHealth healthBar;
        float distFromEnemyCenter = 60;

        Bounds bounds;
        Tank player;

        bool canFire = false;

        new readonly float speed = 200, rotationSpeed = 40 * (MathF.PI / 180), turretRotSpeed = 30 * (MathF.PI / 180);

        public AI(float rotation, Vector2 position, float hp, int maxRange, int idealRange, Tank tank) : base(rotation, position, hp)
        {
            attackDelay = new Timer(1.5f);
            player = tank;

            distFromEnemyCenter = (collider.Position.Distance((collider as BoxCollider).TopLeftPoint) + 10 + 5);
            healthBar = new EnemyHealth(Position + (-Vector2.Up * distFromEnemyCenter), 80, 10);

            bounds = new Bounds(position, maxRange, idealRange);
        }

        public override void OnUpdate(float deltaTime)
        {
            Move(deltaTime);
            RotateTurret(deltaTime);

            // Update Healthbar
            healthBar.Update(deltaTime);

            // Fire bullets
            if (canFire & attackDelay.Check(false))
            {
                float rotation = MathF.Atan2(turretObject.GlobalTransform.m2, turretObject.GlobalTransform.m1);
                rotation = (rotation < 0) ? rotation + (2 * MathF.PI) : rotation;

                Vector2 bulletPos = Position + (new Vector2(turretObject.GlobalTransform.m1, turretObject.GlobalTransform.m2).Normalised() * turretSprite.Height);

                bullets.Add(new Bullet(ref PreLoadedTextures.EnemyBulletTexture, 800, bulletPos, rotation, 2, this));
                attackDelay.Reset();
            }

            // Update Bullets
            for (int x = 0; x < bullets.Count; x++)
            {
                bullets[x].Update(deltaTime);

                if (bullets.Count <= x)
                    continue;

                bullets[x].CheckCollision(player);
            }

            // Alter color when AI gets hit
            tankColor = Color.WHITE;
            if (!hurtTime.Check(false))
            {
                if (hurtTime.Time / (hurtTime.delay / 2) < 1) // Become more red until halfway through timer
                {
                    tankColor = ColorRGB.Lerp(Color.WHITE, hurtColor, hurtTime.Time / (hurtTime.delay / 2));
                }
                else // Become less red from halfway through timer to completion
                {
                    tankColor = ColorRGB.Lerp(hurtColor, Color.WHITE, (hurtTime.Time - (hurtTime.delay / 2)) / ((hurtTime.delay / 2)));
                }
            }
            tankSprite.spriteColor = tankColor;
            turretSprite.spriteColor = tankColor;

            // shift AI position to simulate camera follow (usually you base.onUpdate but can't be used here
            Vector2 tmpVector = Program.Center - Game.CurCenter;
            Translate(tmpVector.x, tmpVector.y);

            // Set position of healthbar and collider
            healthBar.SetPosition(Position.x, Position.y - distFromEnemyCenter);
            collider.SetPosition(Position);
        }

        public override void OnDraw()
        {
            healthBar.Draw();
            base.OnDraw();
        }

        public override void TakeDamage()
        {
            hurtTime.Reset();
            health.CountByValue(1);
            if (health.IsComplete(false)) // AI death
            {
                EnemyManager.RemoveEnemy(this);
                return;
            }
            // Alter healthbar to match current health
            healthBar.SetHealth(health.TimeRemaining / health.delay);
        }

        /// <summary>
        /// Move and rotate AI
        /// </summary>
        void Move(float deltaTime)
        {
            Vector2 desiredDirectionOfTravel = Vector2.Zero;
            if (Position.Distance(player.Position) > bounds.minRadius) // Move towards player if they're too far away
            {
                desiredDirectionOfTravel = player.Position - Position;
            }
            else if (Position.Distance(player.Position) < bounds.minRadius) // Move away from player if they're too close
            {
                desiredDirectionOfTravel = Position - player.Position;
            }

            // Determine if the Ai should try to move
            if (desiredDirectionOfTravel.Magnitude() == 0)
                return;

            desiredDirectionOfTravel.Normalize();

            // Determine forward and perpendicular vectors for math times
            Vector2 forwardDirection = new Vector2(LocalTransform.m1, LocalTransform.m2);
            Vector2 leftDirection = forwardDirection.GetPerpendicular();

            // Calculate angles
            float forwardAngle = MathF.Atan2(desiredDirectionOfTravel.y, desiredDirectionOfTravel.x) - MathF.Atan2(forwardDirection.y, forwardDirection.x);
            float leftAngle = MathF.Atan2(desiredDirectionOfTravel.y, desiredDirectionOfTravel.x) - MathF.Atan2(leftDirection.y, leftDirection.x);

            // Calculate all necessary bools
            bool moveForward = MathF.Abs(forwardAngle) < 0.5f * MathF.PI;
            bool rotateLeft = (MathF.Abs(leftAngle) < 0.5f * MathF.PI) == moveForward;
            bool shouldMove = (moveForward && MathF.Abs(forwardAngle) < 30 * (MathF.PI / 180)) || (!moveForward && MathF.Abs(forwardAngle) > 150 * (MathF.PI / 180));
            bool shouldRotate = MathF.Abs(forwardAngle) > 0.005f;

            if (shouldRotate) // Rotate
            {
                float rotationStep = rotationSpeed * deltaTime * ((rotateLeft) ? -1 : 1);
                rotationStep = (forwardAngle >= rotationStep) ? rotationStep : forwardAngle;
                Rotate(rotationStep);
                collider.Rotate(rotationStep);
            }
            if (shouldMove) // Move
            {
                Vector3 facing = new Vector3(LocalTransform.m1, LocalTransform.m2, 1);
                float mult = deltaTime * speed;
                float multAlt = MathF.Abs(Position.Distance(player.Position) - bounds.minRadius);
                facing *= ((multAlt >= mult) ? mult : multAlt) * ((moveForward) ? 1 : -1);
                Translate(facing.x, facing.y);
            }
        }


        /// <summary>
        /// Rotate the turret
        /// </summary>
        void RotateTurret(float deltaTime)
        {
            // Only rotate if player is in attack range
            if (Position.Distance(player.Position) > bounds.maxRadius)
            {
                canFire = false;
                return;
            }

            Vector2 DesiredAngle = player.Position - Position;

            float angle = MathF.Atan2(DesiredAngle.y, DesiredAngle.x) - MathF.Atan2(turretObject.GlobalTransform.m2, turretObject.GlobalTransform.m1);
            int directionToRotate = (angle < 0) ? -1 : 1;
            angle = MathF.Abs(angle);

            canFire = angle < 5 * (MathF.PI / 180);

            if (angle > 0.005f)
            {
                turretObject.Rotate(turretRotSpeed * deltaTime * directionToRotate);
            }
        }
    }
}
