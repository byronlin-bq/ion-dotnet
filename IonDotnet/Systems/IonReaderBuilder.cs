﻿using System;
using System.IO;
using IonDotnet.Internals.Binary;
using IonDotnet.Internals.Text;

namespace IonDotnet.Systems
{
    /// <summary>
    /// Builder that can generate <see cref="IIonReader"/> instances for different types of input. 
    /// </summary>
    /// <remarks>
    /// Note that Ion readers work with pre-created input streams, so there is no method that accept a byte[]. Callers are
    /// responsible for creating the stream and disposing them. 
    /// </remarks>
    public static class IonReaderBuilder
    {
        /// <summary>
        /// Build a text reader for the string
        /// </summary>
        /// <param name="text">Ion text</param>
        /// <returns>An Ion text reader</returns>
        public static IIonReader Build(string text)
        {
            return new UserTextReader(text);
        }

        /// <summary>
        /// Build an Ion reader for the data stream
        /// </summary>
        /// <param name="stream">Ion data stream in binary of UTF8-text form</param>
        /// <returns>Ion reader</returns>
        public static IIonReader Build(Stream stream)
        {
            if (!stream.CanRead)
                throw new ArgumentException("Stream must not be readable", nameof(stream));

            /* Notes about implementation
               The stream can contain text or binary ion. The ion reader should figure it out. Since we don't want this call to block 
               in case the stream is a network stream or file stream, it can (and should )
               return a wrapper which peeks the stream and check for Bvm, and delegate the rest to the approriate reader.
               Special case is when the stream is a memory stream which can be read directly, in which case we can do the Bvm checking right away.
               Also the Bvm might not be neccessary for the binary reader (except maybe for checking Ion version) so we might end up passing the 
               already-read stream to the binary reader. 
            */

            //this is the dumbed down implementation
            Span<byte> initialBytes = stackalloc byte[BinaryConstants.BinaryVersionMarkerLength];
            var bytesRead = stream.Read(initialBytes);
            var didSeek = stream.CanSeek;
            if (didSeek)
            {
                try
                {
                    stream.Seek(-bytesRead, SeekOrigin.Current);
                }
                catch (IOException)
                {
                    didSeek = false;
                }
            }

            if (IsBinaryData(initialBytes.Slice(bytesRead)))
            {
                //skipping the version marker should be fine for binary reader
                return new UserBinaryReader(stream);
            }

            return didSeek ? new UserTextReader(stream) : new UserTextReader(stream, initialBytes.Slice(bytesRead));
        }

        private static bool IsBinaryData(Span<byte> initialByte)
        {
            //progressively check the binary version marker
            return initialByte.Length >= 4
                   && initialByte[0] == 0xE0
                   && initialByte[1] == 0x01
                   && initialByte[2] == 0x00
                   && initialByte[3] == 0xEA;
        }
    }
}
