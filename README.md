## Deskripsi
Project ini merupakan implementasi bot pada permainan Robocode Tank Royale untuk Tugas Besar 1 Strategi Algoritma. Bot dikembangkan menggunakan bahasa C# dan menerapkan strategi greedy dalam menentukan aksi selama pertempuran berlangsung. Pada permainan Robocode Tank Royale, setiap bot bertarung di arena hingga tersisa satu pemenang (battle royale). Seluruh aksi bot dikendalikan sepenuhnya oleh algoritma yang diprogram oleh pemain tanpa kontrol manual selama pertandingan berlangsung.
## Main Bot
SixSeven
## Alternative Bots
-
-
-

## Algoritma
1. SixSeven menerapkan strategi greedy dengan selalu memilih musuh terdekat sebagai target utama berdasarkan jarak minimum yang terdeteksi oleh radar. Selain itu, bot menggunakan perhitungan risk function untuk menentukan titik pergerakan dengan risiko paling kecil terhadap posisi musuh, sehingga bot dapat bergerak lebih aman sambil tetap menjaga efektivitas serangan menggunakan linear targeting dan adaptive bullet power.

## Cara Menjalankan Program

1. Clone repository ini ke mesin lokal Anda:

`git clone https://github.com/keyyjuya/Tubes_SixSeven`

2. Masuk ke folder project bot:

`cd src/main-bot/SixSeven`

3. Sesuaikan versi .NET pada file SixSeven.csproj dengan versi .NET yang terinstal pada perangkat Anda.

4. Hapus folder bin dan obj jika ada, lalu jalankan:

## Command Prompt
`./SixSeven.cmd`
## Bash
`./SixSeven.sh`

Jalankan aplikasi Robocode Tank Royale, lalu tambahkan folder bot hasil build ke dalam konfigurasi bot directory Robocode dan masukkan bot ke arena pertandingan.

## Author
- Kezia Adelina Tamba (124140046)
- Nathania Calista Hutapea (124140101)
- Sahal Alvin Zairy (124140167)
