using System;
using System.Drawing;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;


public class Top67 : Bot
{
    // Batas energi buat masuk mode defensif
    private const double lowEnergyThreshold = 30.0; 
    
    // Jarak ideal pas ngejar musuh
    private const double trackingDistance = 150; 

    // Toleransi buat ngatur jarak
    private const double tolerance = 20;         

    // Info bot yang ke-scan
    private ScannedBotEvent lastScannedBot = null;

    // Arah nge-strafe (1 = kanan, -1 = kiri)
    private int strafeDirection = 1;
    
    // Ukuran gelombangnya
    private const double WaveAmplitude = 60; 

    // Ngatur seberapa sering gelombang
    private const double WavePeriod = 40; 

    // Batas gerakan biar gak terus-terusan nge-wave
    private const int maxMoves = 50;

    // Jarak pinggiran
    private const double padding = 30;

    // Jarak nembak maksimal
    private const double maxShootingDistance = 500;

    // Sisi-sisi area yang dibatesin
    private double left, right, bottom, top;

    // Power pelurunya
    private double firePower;

    // Status bot
    private bool isDefensiveMode = false;

    static void Main(string[] args)
    {
        new Top67().Start();
    }

    Top67() : base(BotInfo.FromFile("Top67.json")) { }

    public override void Run()
    {
        // Atur warna
        BodyColor = Color.Red;
        TurretColor = Color.Purple;
        RadarColor = Color.Lime;
        BulletColor = Color.White;
        ScanColor = Color.Orange;
        
        // Biar bagian-bagian bisa muter sendiri
        AdjustGunForBodyTurn = true;
        AdjustRadarForBodyTurn = true;
        AdjustRadarForGunTurn = true;
        
        // Tentukan batas area buat mode defensif
        left = padding;
        right = ArenaWidth - padding;
        bottom = padding;
        top = ArenaHeight - padding;
        
        while (IsRunning)
        {
            // Cek mode defensif dari energi
            isDefensiveMode = Energy < lowEnergyThreshold;

            // Nge-scan terus
            SetTurnRadarRight(360);
            SetTurnRadarLeft(360);

            if (lastScannedBot == null)
            {
                // Kalau belum ada target, scan terus 
                SetTurnRadarRight(360);
                SetTurnRadarLeft(360);
            }
            
            // Gerak defensif kalo energi tipis
            if (isDefensiveMode)
            {
                if (lastScannedBot != null) TrackTargetRadarOnly();
                GoToNearestMiddleOfASide();
                WaitFor(new MovementCompleteCondition(this));
                MoveWavySide();
            }

            // Kalau enggak, ngejar biasa
            else
            {
                if (lastScannedBot != null) TrackTarget();
                else SetTurnRadarRight(360);
                Go();
            }
        }
    }
    
    // Ngejar target sambil strafing dan jaga jarak
    private void TrackTarget()
    {
        if (lastScannedBot == null) return;

        double enemyX = lastScannedBot.X;
        double enemyY = lastScannedBot.Y;
        double angleToEnemy = Math.Atan2(enemyY - Y, enemyX - X) * 180 / Math.PI;
        double distanceToEnemy = Math.Sqrt((enemyX - X) * (enemyX - X) + (enemyY - Y) * (enemyY - Y));

        // Arahkan meriam ke musuh
        double gunTurn = NormalizeRelativeAngle(angleToEnemy - GunDirection);
        SetTurnGunLeft(gunTurn);

        // Sesuaikan jarak kalo belum pas
        double distanceError = distanceToEnemy - trackingDistance;
        if (Math.Abs(distanceError) > tolerance)
        {
            if (distanceError > 0)
            {
                SetForward(Math.Min(distanceError, 100));
            }
            else
            {
                SetBack(Math.Min(-distanceError, 100));
            }
        }

        // Kalo udah pas, strafing aja
        else
        {
            double desiredStrafeAngle = NormalizeAbsoluteAngle(angleToEnemy + 90 * strafeDirection);
            double turnToStrafe = NormalizeRelativeAngle(desiredStrafeAngle - Direction);

            SetTurnRight(turnToStrafe);
            SetForward(70);
        }
    }
    
    // Nge-track pake radar + gun doang (mode defensif)
    private void TrackTargetRadarOnly()
    {
        if (lastScannedBot == null) return;

        double enemyX = lastScannedBot.X;
        double enemyY = lastScannedBot.Y;
        double angleToEnemy = Math.Atan2(enemyY - Y, enemyX - X) * 180 / Math.PI;

        // Arahkan meriam ke musuh
        double gunTurn = NormalizeRelativeAngle(angleToEnemy - GunDirection);
        SetTurnGunLeft(gunTurn);
        
        // Kunci radar ke musuh, sweep lebih lebar biar gak lepas
        double radarTurn = NormalizeRelativeAngle(angleToEnemy - RadarDirection);
        SetTurnRadarLeft(radarTurn * 2);
    }
    
    // Biar gerak ke tengah sisi terdekat area
    private void GoToNearestMiddleOfASide()
    {
        double middleX = (left + right) / 2;
        double middleY = (bottom + top) / 2;

        // Cari jauh ke tengah tiap sisi pake DistanceTo
        double dLeftSide = DistanceTo(left, middleY);
        double dRightSide = DistanceTo(right, middleY);
        double dBottomSide = DistanceTo(middleX, bottom);
        double dTopSide = DistanceTo(middleX, top);
        
        // Tentukan sisi mana paling deket
        double minDist = dLeftSide;
        string nearestSide = "left";
        if (dRightSide < minDist) 
        { 
            minDist = dRightSide; 
            nearestSide = "right"; 
        }

        if (dBottomSide < minDist) 
        { 
            minDist = dBottomSide; 
            nearestSide = "bottom"; 
        }

        if (dTopSide < minDist) 
        { 
            minDist = dTopSide; 
            nearestSide = "top"; 
        }
        
        // Cek kalo udah di tengah sisi (jarak nyaris 0)
        bool alreadyAtMiddle = minDist < 0.001;
        
        if (nearestSide == "left")
        {
            // Belok ke tengah sisi kiri, ngadep ke timur laut
            if (!alreadyAtMiddle)
            {
                double angle = Math.Atan2(middleY - Y, left - X) * 180 / Math.PI;
                TurnToCardinalDirection(angle);
                Forward(minDist);
                WaitFor(new MovementCompleteCondition(this));
            }

            TurnToCardinalDirection(45);
        }
        else if (nearestSide == "right")
        {
            // Belok ke tengah sisi kanan, ngadep ke barat daya
            if (!alreadyAtMiddle)
            {
                double angle = Math.Atan2(middleY - Y, right - X) * 180 / Math.PI;
                TurnToCardinalDirection(angle);
                Forward(minDist);
                WaitFor(new MovementCompleteCondition(this));
            }

            TurnToCardinalDirection(225);
        }
        else if (nearestSide == "bottom")
        {
            // Belok ke tengah sisi bawah, ngadep ke barat laut
            if (!alreadyAtMiddle)
            {
                double angle = Math.Atan2(bottom - Y, middleX - X) * 180 / Math.PI;
                TurnToCardinalDirection(angle);
                Forward(minDist);
                WaitFor(new MovementCompleteCondition(this));
            }

            TurnToCardinalDirection(135);
        }
        else // (nearestSide == "top")
        {
            
            // Belok ke tengah sisi atas, ngadep ke tenggara
            if (!alreadyAtMiddle)
            {
                double angle = Math.Atan2(top - Y, middleX - X) * 180 / Math.PI;
                TurnToCardinalDirection(angle);
                Forward(minDist);
                WaitFor(new MovementCompleteCondition(this));
            }
            TurnToCardinalDirection(315);
        }
    }
    
    // Bantuin muter ke arah cardinal yang dituju
    private void TurnToCardinalDirection(double targetDirection)
    {
        double turnAmount = NormalizeRelativeAngle(targetDirection - Direction);
        TurnLeft(turnAmount);
        WaitFor(new TurnCompleteCondition(this));
    }

    // Jalan di satu sisi kotak bergelombang
    private void MoveWavySide()
    {
        // Cek dulu kalo keluar batas, terus beresin
        if (X < left || X > right || Y < bottom || Y > top)
        {
            HandleOutOfBounds();
            return;
        }

        // Catet posisi gelombang sekarang
        double wavePosition = 0;
        double stepSize = 5;
        int moveCounter = 0;
        
        double distanceX = X - left;
        double distanceY = Y - bottom;
        
        while (distanceX > -3 && 
                distanceY > -3 && 
                distanceX < right - left + 3 && 
                distanceY < top - bottom + 3 && 
                moveCounter < maxMoves)
        {
            // Hitung sudut gelombang
            double waveAngle = Math.Sin(wavePosition / WavePeriod) * WaveAmplitude;
            
            // Belok ngikutin pola wave
            SetTurnRight(waveAngle);
            SetForward(stepSize);
            Go();

            distanceX = X - left;
            distanceY = Y - bottom;

            wavePosition += stepSize;
            moveCounter++;
        }
    }
    
    // Beresin kondisi keluar batas
    private void HandleOutOfBounds()
    {
        // Berhentiin gerakan dulu
        Stop();
        
        double targetDirection = -1;
        
        // Cek batas mana yang terlewati
        if      (X < left && Y < bottom) targetDirection = 45;      // Northeast
        else if (X < left && Y > top) targetDirection = 315;        // Southeast
        else if (X > right && Y < bottom) targetDirection = 135;    // Northwest
        else if (X > right && Y > top) targetDirection = 225;       // Southwest
        else if (X < left) targetDirection = 0;                     // East
        else if (X > right) targetDirection = 180;                  // West
        else if (Y < bottom) targetDirection = 90;                  // North
        else if (Y > top) targetDirection = 270;                    // South
        
        if (targetDirection != -1)
        {
            double turnAmount = NormalizeRelativeAngle(targetDirection - Direction);
            
            SetTurnLeft(turnAmount);
            Go();
            WaitFor(new TurnCompleteCondition(this));
            
            double moveDistance = 40;
            SetForward(moveDistance);
            Go();
            WaitFor(new MovementCompleteCondition(this));
        }
    }

    // Nanganin bot yang ke-scan pake strategi hybrid targeting
    public override void OnScannedBot(ScannedBotEvent e)
    {
        // Pilih target yang paling gampang: musuh energi paling rendah atau target yang lagi di-track
        if (lastScannedBot == null || e.ScannedBotId == lastScannedBot.ScannedBotId || e.Energy < lastScannedBot.Energy)
        {
            lastScannedBot = e;
        }
        
        double distance = DistanceTo(e.X, e.Y);
        double angleToEnemy = Math.Atan2(e.Y - Y, e.X - X) * 180 / Math.PI;
        double gunTurn = NormalizeRelativeAngle(angleToEnemy - GunDirection);

        // Kunci radar ke target dengan sedikit overcorrection
        double radarTurn = NormalizeRelativeAngle(angleToEnemy - RadarDirection);
        SetTurnRadarLeft(radarTurn * 2);

        if (distance > maxShootingDistance)
        {
            // Tetep ngarahin meriam, tapi belum nembak
            SetTurnGunLeft(gunTurn);
            return;
        }
    
        // Tentuin power nembak dari beberapa faktor
        firePower = calculateFirePower(distance);
        if (Energy > e.Energy + 30) firePower = Math.Min(firePower, Energy / 10);
        if (e.Energy < 16)
        {
            if (distance < 100) firePower = 3.0;
            else if (distance < 300) firePower = 2.5;
        };
        firePower = Math.Min(firePower, Energy);

        // Hitung kecepatan peluru dan waktu nembak
        double bulletSpeed = 20 - 3 * firePower;
        double timeToHit = distance / bulletSpeed;
        
        // Tebak posisi musuh nanti
        double enemyHeadingRadians = e.Direction * Math.PI / 180; 
        double predictedX = e.X + Math.Cos(enemyHeadingRadians) * e.Speed * timeToHit;
        double predictedY = e.Y + Math.Sin(enemyHeadingRadians) * e.Speed * timeToHit;
        
        // Hitung sudut ke posisi prediksi
        double angleToEnemyPredicted = Math.Atan2(predictedY - Y, predictedX - X) * 180 / Math.PI;
        double gunTurnPredicted = NormalizeRelativeAngle(angleToEnemyPredicted - GunDirection);
        SetTurnGunLeft(gunTurnPredicted);
        
        // Hitung seberapa pede aim-nya
        double aimConfidence;

        // Pede banget kalo deket
        if (distance < 50) aimConfidence = 0.95; 
        else 
        {
            aimConfidence = 1.0;
            aimConfidence *= 1.0 - (distance / 1000.0);                                // Distance factor
            aimConfidence *= 1.0 - (Math.Min(8, e.Speed) / 16.0);                      // Speed factor
            aimConfidence *= 1.0 - (Math.Min(45, Math.Abs(gunTurnPredicted)) / 120.0); // Gun turn factor
        }
        
        // Tentukan error sudut meriam yang masih boleh
        double maxGunAngleError = distance < 50 ? 25 : 15 * aimConfidence;
        
        // Nembak kalo udah pede dan gun siap
        if (Math.Abs(gunTurnPredicted) < maxGunAngleError && (aimConfidence > 0.3) && GunHeat == 0)
        {
            // Close combat masih pake full power, jauh sedikit dikit powernya
            double adjustedFirePower = distance < 50 ? firePower : firePower * Math.Max(0.8, aimConfidence);
            Fire(adjustedFirePower);
        }
    }

    // Tentuin power nembak dari faktor jarak
    public double calculateFirePower(double distance)
    {
        firePower = 1.0;                        

        if      (distance <= 50)  firePower = 3.0;      // Jarak dekat: power maksimal
        else if (distance <= 150) firePower = 2.5;      // Dekat-medium: power tinggi
        else if (distance <= 300) firePower = 2.0;      // Medium: power sedang
        else if (distance <= 450) firePower = 1.5;      // Jauh-medium: power moderate

        return firePower;
    }

    // Ngindar pas kena peluru
    public override void OnHitByBullet(HitByBulletEvent e)
    {
        // Ganti arah strafe biar ngindar
        strafeDirection *= -1;
    }

    // Nanganin tabrakan sama bot lain yang lebih oke
    public override void OnHitBot(HitBotEvent e)
    {
        double distance = DistanceTo(e.X, e.Y);
        double bearing = BearingTo(e.X, e.Y);
        
        // Kalo ditabrak bot lain
        if (e.IsRammed) 
        {
            Stop();
            
            // Puter meriam ke musuh dan nembak sesuai jarak
            double gunTurn = NormalizeRelativeAngle(bearing - GunDirection);
            TurnGunLeft(gunTurn);
            
            // Nembak dengan power sesuai jarak
            firePower = calculateFirePower(distance);
            Fire(firePower);
        }

        // Kalo kita nabrak bot lain
        else 
        {
            Stop();
            
            double gunTurn = NormalizeRelativeAngle(bearing - GunDirection);
            TurnGunLeft(gunTurn);
            
            // Nembak dengan power sesuai jarak
            firePower = calculateFirePower(distance);
            Fire(firePower);
        }
    }

    // Kalo nabrak tembok, balik arah biar aman
    public override void OnHitWall(HitWallEvent e)
    {
        if (isDefensiveMode)
        {
            Stop();
            
            bool hitLeftWall = X <= left + 5;
            bool hitRightWall = X >= right - 5;
            bool hitBottomWall = Y <= bottom + 5;
            bool hitTopWall = Y >= top - 5;
            
            double retreatDirection = Direction;
            
            if      (hitLeftWall) retreatDirection = 0;     // Move east
            else if (hitRightWall) retreatDirection = 180;  // Move west
            else if (hitBottomWall) retreatDirection = 90;  // Move north
            else if (hitTopWall) retreatDirection = 270;    // Move south
            
            double turnAmount = NormalizeRelativeAngle(retreatDirection - Direction);
            
            SetTurnLeft(turnAmount);
            Go();
            WaitFor(new TurnCompleteCondition(this));
            
            SetForward(50);
            Go();
            WaitFor(new MovementCompleteCondition(this));
            
            GoToNearestMiddleOfASide();
        }
        else
        {
            // Balikin arah strafe pas nabrak tembok biar gak macet
            strafeDirection *= -1;
            SetTurnRight(90);
        }
    }
}

// Kondisi yang aktif pas putaran selesai
public class TurnCompleteCondition : Condition
{
    private readonly Bot bot;

    public TurnCompleteCondition(Bot bot)
    {
        this.bot = bot;
    }

    public override bool Test() => bot.TurnRemaining == 0;
}

// Kondisi yang aktif pas gerakan selesai
public class MovementCompleteCondition : Condition
{
    private readonly Bot bot;

    public MovementCompleteCondition(Bot bot)
    {
        this.bot = bot;
    }

    public override bool Test() => bot.DistanceRemaining == 0;
}