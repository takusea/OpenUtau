﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using K4os.Hash.xxHash;
using NAudio.Wave;
using OpenUtau.Core;
using OpenUtau.Core.Render;
using OpenUtau.Core.Ustx;
using static OpenUtau.Api.Phonemizer;

namespace OpenUtau.Classic {
    public class ResamplerItem {
        public RenderPhrase phrase;
        public RenderPhone phone;

        public IResampler resampler;
        public string inputFile;
        public string inputTemp;
        public string outputFile;
        public int tone;

        public Tuple<string, int?, string>[] flags;//flag, value, abbr
        public int velocity;
        public int volume;
        public int modulation;

        public float preutter;
        public float overlap;
        public double offset;
        public double durRequired;
        public double durCorrection;
        public double consonant;
        public double cutoff;
        public double skipOver;

        public double tempo;
        public int[] pitches;

        public ulong hash;

        public ResamplerItem(RenderPhrase phrase, RenderPhone phone) {
            this.phrase = phrase;
            this.phone = phone;

            resampler = ToolsManager.Inst.GetResampler(phone.resampler);
            inputFile = phone.oto.File;
            inputTemp = VoicebankFiles.Inst.GetSourceTempPath(phrase.singer.Id, phone.oto, ".wav");
            tone = phone.tone;

            flags = phone.flags.Where(flag => resampler.SupportsFlag(flag.Item3)).ToArray();
            velocity = (int)(phone.velocity * 100);
            volume = (int)(phone.volume * 100);
            modulation = (int)(phone.modulation * 100);

            preutter = (float)phone.preutterMs;
            overlap = (float)phone.overlapMs;
            offset = phone.oto.Offset;
            var stretchRatio = Math.Pow(2, 1.0 - velocity * 0.01);
            double pitchLeadingMs = phone.oto.Preutter * stretchRatio;
            skipOver = phone.oto.Preutter * stretchRatio - phone.leadingMs;
            durRequired = phone.endMs - phone.positionMs + phone.durCorrectionMs + skipOver;
            durRequired = Math.Max(durRequired, phone.oto.Consonant);
            durRequired = Math.Ceiling(durRequired / 50.0 + 0.5) * 50.0;
            durCorrection = phone.durCorrectionMs;
            consonant = phone.oto.Consonant;
            cutoff = phone.oto.Cutoff;

            tempo = phone.adjustedTempo;

            double pitchCountMs = (phone.positionMs + phone.envelope[4].X) - (phone.positionMs - pitchLeadingMs);
            int pitchCount = (int)Math.Ceiling(MusicMath.TempoMsToTick(tempo, pitchCountMs) / 5.0);
            pitchCount = Math.Max(pitchCount, 0);
            pitches = new int[pitchCount];

            double phoneStartMs = phone.positionMs - pitchLeadingMs;
            double phraseStartMs = phrase.positionMs - phrase.leadingMs;
            for (int i = 0; i < phone.tempos.Length; i++) {
                double startMs = Math.Max(phrase.timeAxis.TickPosToMsPos(phone.tempos[i].position), phoneStartMs);
                double endMs = i + 1 < phone.tempos.Length ? phrase.timeAxis.TickPosToMsPos(phone.tempos[i + 1].position) : phone.positionMs + phone.envelope[4].X;
                double durationMs = endMs - startMs;
                int tempoPitchCount = (int)Math.Floor(MusicMath.TempoMsToTick(tempo, durationMs) / 5.0);
                int tempoPitchSkip = (int)Math.Floor(MusicMath.TempoMsToTick(tempo, startMs - phoneStartMs) / 5.0);
                tempoPitchCount = Math.Min(tempoPitchCount, pitches.Length - tempoPitchSkip);
                int phrasePitchSkip = (int)Math.Floor(phrase.timeAxis.TicksBetweenMsPos(phraseStartMs, startMs) / 5.0);
                double intervalPitchMs = 120 / tempo * 500 / 480 * 5;
                double diffPitchMs = startMs - phraseStartMs - phrase.timeAxis.TickPosToMsPos(phrasePitchSkip * 5);
                double tempoRatio = phone.tempos[i].bpm / tempo;
                for (int j = 0; j < tempoPitchCount; j++) {
                    int index = tempoPitchSkip + j;
                    int scaled = phrasePitchSkip + (int)Math.Ceiling(j * tempoRatio);
                    scaled = Math.Clamp(scaled, 0, phrase.pitches.Length - 1);
                    int nextScaled = Math.Clamp(scaled + 1, 0, phrase.pitches.Length - 1);
                    index = Math.Clamp(index, 0, pitchCount - 1);
                    pitches[index] = (int)Math.Round((phrase.pitches[nextScaled]- phrase.pitches[scaled]) /intervalPitchMs * diffPitchMs + phrase.pitches[scaled] - phone.tone * 100);
                }
            }

            hash = Hash();
            outputFile = Path.Join(PathManager.Inst.CachePath,
                $"res-{XXH32.DigestOf(Encoding.UTF8.GetBytes(phrase.singer.Id)):x8}-{hash:x16}.wav");
        }
        public string GetFlagsString() {
            var builder = new StringBuilder();
            foreach (var flag in flags) {
                builder.Append(flag.Item1);
                if (flag.Item2.HasValue) {
                    builder.Append(flag.Item2.Value);
                }
            }
            return builder.ToString();
        }

        ulong Hash() {
            using (var stream = new MemoryStream()) {
                using (var writer = new BinaryWriter(stream)) {
                    writer.Write(resampler.ToString());
                    writer.Write(inputFile);
                    writer.Write(tone);

                    foreach (var flag in flags) {
                        writer.Write(flag.Item1);
                        if (flag.Item2.HasValue) {
                            writer.Write(flag.Item2.Value);
                        }
                    }
                    writer.Write(velocity);
                    writer.Write(volume);
                    writer.Write(modulation);

                    writer.Write(offset);
                    writer.Write(durRequired);
                    writer.Write(consonant);
                    writer.Write(cutoff);
                    writer.Write(skipOver);

                    writer.Write(tempo);
                    foreach (int pitch in pitches) {
                        writer.Write(pitch);
                    }
                    return XXH64.DigestOf(stream.ToArray());
                }
            }
        }
    }
}
