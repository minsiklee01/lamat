using System;
using System.IO;
using System.Media;
using System.Text;
using System.Windows.Media;

namespace lamat.Services
{
    // Background music (MediaPlayer, looped) + a synthesized key-click effect (no external asset needed).
    public class SoundService
    {
        private readonly MediaPlayer _musicPlayer = new();
        private SoundPlayer? _clickPlayer;
        private bool _musicFileFound;
        private bool _isMuted;

        public bool IsMuted => _isMuted;

        public void Initialize(string basePath)
        {
            _clickPlayer = new SoundPlayer(GenerateClickWav());
            _clickPlayer.Load();

            string musicPath = Path.Combine(basePath, "Data", "Audio", "background-music.mp3");
            _musicFileFound = File.Exists(musicPath);
            if (_musicFileFound)
            {
                _musicPlayer.Open(new Uri(musicPath));
                _musicPlayer.MediaEnded += (_, _) =>
                {
                    _musicPlayer.Position = TimeSpan.Zero;
                    _musicPlayer.Play();
                };
            }
        }

        public void StartMusic()
        {
            if (_musicFileFound && !_isMuted)
                _musicPlayer.Play();
        }

        public void ToggleMute()
        {
            _isMuted = !_isMuted;
            if (_isMuted)
            {
                _musicPlayer.Pause();
            }
            else if (_musicFileFound)
            {
                _musicPlayer.Play();
            }
        }

        public void PlayClick()
        {
            if (_isMuted) return;
            _clickPlayer?.Play();
        }

        // Short percussive "key click": an exponentially-decaying tone + noise burst,
        // rendered as a 16-bit mono PCM WAV in memory so no bundled asset is required.
        private static Stream GenerateClickWav()
        {
            const int sampleRate = 44100;
            const double durationSec = 0.045;
            int sampleCount = (int)(sampleRate * durationSec);
            var samples = new short[sampleCount];
            var rng = new Random(12345);

            for (int i = 0; i < sampleCount; i++)
            {
                double t = (double)i / sampleRate;
                double envelope = Math.Exp(-t * 90.0);
                double tone = Math.Sin(2 * Math.PI * 1400 * t) * 0.5;
                double noise = (rng.NextDouble() * 2 - 1) * 0.5;
                double sample = envelope * (tone + noise);
                samples[i] = (short)Math.Clamp(sample * short.MaxValue, short.MinValue, short.MaxValue);
            }

            var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                int byteRate = sampleRate * 2;
                int dataSize = sampleCount * 2;
                writer.Write("RIFF".ToCharArray());
                writer.Write(36 + dataSize);
                writer.Write("WAVE".ToCharArray());
                writer.Write("fmt ".ToCharArray());
                writer.Write(16);
                writer.Write((short)1);  // PCM
                writer.Write((short)1);  // mono
                writer.Write(sampleRate);
                writer.Write(byteRate);
                writer.Write((short)2);  // block align
                writer.Write((short)16); // bits per sample
                writer.Write("data".ToCharArray());
                writer.Write(dataSize);
                foreach (short s in samples)
                    writer.Write(s);
            }
            stream.Position = 0;
            return stream;
        }
    }
}
