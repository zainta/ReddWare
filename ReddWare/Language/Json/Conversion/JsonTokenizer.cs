// Copyright (c) Zain Al-Ahmary.  All rights reserved.
// Licensed under the MIT License, (the "License"); you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at https://mit-license.org/

using ReddWare.Collections;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;

namespace ReddWare.Language.Json.Conversion
{
    /// <summary>
    /// Breaks a JSON string down into its component tokens
    /// </summary>
    class JsonTokenizer : TokenizerBase<JsonToken>
    {
        private bool _alterStringEscapes;
        private int _chunkingIndex;
        private bool _appendEndMarker;

        /// <summary>
        /// Create a JsonTokenizer
        /// </summary>
        /// <param name="index">The index of the chunk tokenizing set this tokenizer occupies</param>
        /// <param name="appendEndMarker">Whether to append the EndOfJSON token at the end of the derived token set</param>
        public JsonTokenizer(int index = -1, bool appendEndMarker = true) : base()
        {
            _chunkingIndex = index;
            _appendEndMarker = appendEndMarker;
        }

        IEnumerable<string> Chunk(string str, int chunkSize)
        {
            if (chunkSize > 0 && !string.IsNullOrWhiteSpace(str))
            {
                return Enumerable.Range(0, str.Length / chunkSize)
                    .Select(i => str.Substring(i * chunkSize, chunkSize));
            } else
            {
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// The tokenize the json
        /// </summary>
        /// <param name="json">The json string to tokenize</param>
        /// <param name="alterStringEscapes">If true, removes backslash escapes, otherwise leaves them alone</param>
        /// <param name="canSubChunk">If true, the Tokenizer can split the workload into smaller chunks and multithread the process to make it faster</param>
        /// <returns></returns>
        public LogItemBase[] Tokenize(string json, bool alterStringEscapes, bool canSubChunk = true)
        {
            // Minimum length of json before chunking can occur
            const int MinimumChunkingLength = 200000;

            // Size of each chunk
            const int ChunkSize = 80000;

            Tokens.Clear();
            Outcome.Clear();
            _buffer.Clear();
            _col = Minimum_Column;
            _row = Minimum_Row;
            _alterStringEscapes = alterStringEscapes;

            // in the event of a string millions of characters long,
            // that string needs to be broken into chunks and each fed into a sub-thread

            if (json.Length > MinimumChunkingLength && canSubChunk)
            {
                // if we have a ton of work, break it down,
                // make sure it's properly split, and then run each chunk in a thread
                // when chunking, only matching double quotes (") matter for string declarations
                // so:
                //  a double quote must not be followed by a comma or colon (as that implies the end of a string)
                //  and double quote escaping must be accounted for
                var chunks = GetProperChunks(json, ChunkSize);
                var remerge = string.Join(string.Empty, chunks.OrderBy(c => c.Item2).SelectMany(c => c.Item1).ToList());

                var list = new List<Tuple<ListStack<JsonToken>, LogItemBase[], int>>();
                var progress = Parallel.ForEach(chunks, (set) =>
                {
                    var tnizer = new JsonTokenizer(set.Item2, false);
                    var r = tnizer.Tokenize(set.Item1, _alterStringEscapes, false);

                    list.Add(new Tuple<ListStack<JsonToken>, LogItemBase[], int>(tnizer.Tokens, r, set.Item2));
                });

                var sortedSets = list.OrderBy(l => l.Item3).ToArray();
                if (list.Any(item => item.Item2.Length > 0))
                {
                    Outcome.AddRange(sortedSets.SelectMany(item => item.Item2).ToList());
                }
                else
                {
                    var merge = sortedSets.SelectMany(item => item.Item1.ToList());

                    // once all of the processing is done,
                    // concatenate all of the lists back together *in the correct order*
                    Tokens.AddRange(merge);
                }
            }
            else
            {
                _buffer.AddRange(json);

                // Loop through the code and pick out the tokens one by one, in order of discovery
                while (!_buffer.Empty)
                {
                    if (!_buffer.Empty && GetJsonFraming())
                    {
                        continue;
                    }
                    else if (!_buffer.Empty && GetString())
                    {
                        continue;
                    }
                    else if (!_buffer.Empty && GetNumbers())
                    {
                        continue;
                    }
                    else if (!_buffer.Empty && GetBoolean())
                    {
                        continue;
                    }
                    else if (!_buffer.Empty)
                    {
                        Outcome.Add(new LogItemBase(_col, _row, $"Unknown character '{Peek()}'."));
                    }
                }
            }

            if (_appendEndMarker)
            {
                Tokens.Add(new JsonToken(JsonTokenTypes.EndOfJSON, ' ', ' ', _col, _row));
            }

            return Outcome.ToArray();
        }

        /// <summary>
        /// Checks a sub-section of a given string against a regular expression
        /// </summary>
        /// <param name="str">The string to check</param>
        /// <param name="pattern">The pattern to use</param>
        /// <param name="startSkip">The number of characters at the start to skip</param>
        /// <param name="endIgnore">The number of characters at the end to ignore</param>
        /// <returns>True upon match, false otherwise</returns>
        private bool IsMatch(string str, string pattern, int startSkip = 1, int endIgnore = 1)
        {
            if ((str.Length < (str.Length - endIgnore)) || startSkip >= str.Length) return false;

            return System.Text.RegularExpressions.Regex.IsMatch(
                str.Substring(startSkip, str.Length - endIgnore),
                pattern);
        }

        /// <summary>
        /// Retrieves all Json framing (characters that describe the structure of the document and now what's in it)
        /// </summary>
        /// <returns></returns>
        private bool GetJsonFraming()
        {
            if (Peek() == '{')
            {
                Tokens.Add(new JsonToken(JsonTokenTypes.CurlyOpen, Peek(), Pop(), _col, _row));
            }
            else if (Peek() == '}')
            {
                Tokens.Add(new JsonToken(JsonTokenTypes.CurlyClose, Peek(), Pop(), _col, _row));
            }
            else if (Peek() == '[')
            {
                Tokens.Add(new JsonToken(JsonTokenTypes.SquareOpen, Peek(), Pop(), _col, _row));
            }
            else if (Peek() == ']')
            {
                Tokens.Add(new JsonToken(JsonTokenTypes.SquareClose, Peek(), Pop(), _col, _row));
            }
            else if (Peek() == ':')
            {
                Tokens.Add(new JsonToken(JsonTokenTypes.Colon, Peek(), Pop(), _col, _row));
            }
            else if (Peek() == ',')
            {
                Tokens.Add(new JsonToken(JsonTokenTypes.Comma, Peek(), Pop(), _col, _row));
            }
            else if (More(4) && PeekStr(length: 4).ToLower() == "null" )
            {
                Tokens.Add(new JsonToken(JsonTokenTypes.Null, PeekStr(length: 4), PopStr(length: 4), _col, _row));
            }
            else if (char.IsWhiteSpace(Peek()))
            {
                // We don't care about whitespace.
                // This is JSON and we aren't expected to reproduce the exact formatting here.
                // that's the formatter's job
                // just trash it.

                while (More() && char.IsWhiteSpace(Peek()))
                {
                    Pop();
                }
            }
            else
            {
                return false;
            }

            return true;
        }
        
        /// <summary>
        /// Gathers a string token and add it to the list
        /// </summary>
        /// <returns>Whether or not a token was generated (if not, implies an error)</returns>
        bool GetString()
        {
            string[] str = null;
            switch (Peek())
            {
                case '\'':
                    str = GetPairedSet('\'', '\'', null);
                    break;
                case '"':
                    str = GetPairedSet('"', '"', null);
                    break;
            }

            if (str != null)
            {
                if (_alterStringEscapes)
                {
                    // when json strings are sent out, backslashes are escaped "\".  We will undo that here.
                    str[0] = str[0].Replace("\\\\", "\\");
                    str[1] = str[1].Replace("\\\\", "\\");
                }

                // check what kind of string it is
                if (IsMatch(str[1], "\\$[Tt][Yy][Pp][Ee]"))
                {
                    Tokens.Add(new JsonToken(JsonTokenTypes.TypeAnnotation, str[1], str[0], _row, _col));
                    return true;
                }
                else if (IsMatch(str[1], "[Tt][Rr][Uu][Ee]") || IsMatch(str[1], "[Ff][Aa][Ll][Ss][Ee]"))
                {
                    Tokens.Add(new JsonToken(JsonTokenTypes.Boolean, str[1], str[0], _row, _col));
                    return true;
                }
                else
                {
                    Tokens.Add(new JsonToken(JsonTokenTypes.String, str[1], str[0], _row, _col));
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gathers a boolean token and add it to the list
        /// </summary>
        /// <returns>Whether or not a token was generated (if not, implies an error)</returns>
        bool GetBoolean()
        {
            if (PeekStr(0, true.ToString().Length).Equals("true", StringComparison.InvariantCultureIgnoreCase))
            {
                var origCol = _col;
                var origRow = _row;
                var val = PopStr(length: true.ToString().Length);
                Tokens.Add(new JsonToken(JsonTokenTypes.Boolean, val, val, origCol, origRow));
                return true;
            }
            else if (PeekStr(0, false.ToString().Length).Equals("false", StringComparison.InvariantCultureIgnoreCase))
            {
                var origCol = _col;
                var origRow = _row;
                var val = PopStr(length: false.ToString().Length);
                Tokens.Add(new JsonToken(JsonTokenTypes.Boolean, val, val, origCol, origRow));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gathers a numerical (whole and real) token and add it to the list
        /// </summary>
        /// <returns>Whether or not a token was generated (if not, implies an error)</returns>
        bool GetNumbers()
        {
            var number = new StringBuilder();
            var done = false;
            var decimaled = false;

            // check if this is a negative number
            if (More(1) && Peek() == '-' &&
                    char.IsDigit(Peek(1)))
            {
                number.Append("-");
                Pop();
            }

            while (!done)
            {
                if (More() && !char.IsDigit(Peek()))
                {
                    done = true;
                }
                else if (_buffer.Empty)
                {
                    if (number.Length > 0)
                    {
                        done = true;
                    }
                    else
                    {
                        Outcome.Add(new LogItemBase(_col, _row, "End of file instead of digit."));
                        return false;
                    }
                }
                else if (More() && Peek() == '.')
                {
                    if (!decimaled)
                    {
                        decimaled = true;
                        if (_buffer.Count > 1 && char.IsDigit(Peek(1))) // make sure there is a number after the decimal point
                        {
                            number.Append(PopStr(0, 2));
                        }
                        else if (_buffer.Count == 1) // there is a just a deicmal point with nothing after it
                        {
                            number.Append(Pop());
                            number.Append('0'); // add a free 0 after the decimal point
                        }
                    }
                    else
                    {
                        Outcome.Add(new LogItemBase(_col, _row, "Duplicate decimal point."));
                        return false;
                    }
                }
                else if (char.IsDigit(Peek()))
                {
                    number.Append(Pop());
                }
            }

            if (number.Length > 0)
            {
                Tokens.Add(new JsonToken(decimaled ? JsonTokenTypes.RealNumber : JsonTokenTypes.WholeNumber, number.ToString(), number.ToString(), _row, _col));
                return true;
            }
            return false;
        }

        /// <summary>
        /// Properly breaks the given json into ~80k character chunks of text, taking strings into account
        /// </summary>
        /// <param name="json">The json to divide</param>
        /// <param name="chunkSize">The size of each chunk</param>
        /// <returns></returns>
        Tuple<string, int>[]? GetProperChunks(string json, int chunkSize)
        {
            var chunks = Chunk(json, chunkSize).Select((chunk, index) => new Tuple<string, int>(chunk, index)).ToArray();
            var total = chunks.Select(tpl => tpl.Item1.Length).Sum();
            chunks = chunks.Append(new Tuple<string, int>(json.Substring(total), chunks.Length)).ToArray();
            int cindex = -1;

            // Ensure that all strings are completely in a single chunk
            while (true)
            {
                cindex++;
                if (cindex >= chunks.Length)
                {
                    break;
                }

                ChunkQuoteEnum quote = ChunkQuoteEnum.Unknown;
                string t = chunks[cindex].Item1;

                int qI = t.LastIndexOf('"');
                if (qI == -1)
                {
                    // there are no double quotes.  Nothing to see here.
                    continue;
                }
                else if (qI >= 0)
                {
                    quote = GetQuoteEscapeStatus(t, qI);
                    if (quote == ChunkQuoteEnum.Valid || quote == ChunkQuoteEnum.Escaped)
                    {
                        // check to see if we have the character after it
                        if (t.Length > (qI + 1))
                        {
                            // we do.  check if that following character is a string follower (, or :)
                            if (t[qI + 1] == ',' || t[qI + 1] == ':' || char.IsWhiteSpace(t[qI + 1]) || t[qI + 1] == '}')
                            {
                                // yes.  done.
                                continue;
                            } 
                            else
                            {
                                // no.  we have an orphaned string!
                                // search the chunks until we find the next valid double quote (terminator)
                                // abduct everything up to it (including it) and the following character (should be a follower)
                                // append them to the end of this chunk, update, and continue
                                bool found = false;
                                StringBuilder chunkAddition = new StringBuilder();
                                int sindex = cindex + 1;
                                ChunkQuoteEnum quote2 = ChunkQuoteEnum.Unknown;
                                while (!found)
                                {
                                    if (chunks.Length > sindex)
                                    {
                                        string nt = chunks[sindex].Item1;
                                        int nqI = -1;
                                        nqI = nt.IndexOf('"', nqI + 1);

                                        if (nqI == -1)
                                        {
                                            // this chunk has no double quote.  we need to look in the next one...
                                            sindex++;

                                            // this chunk gets removed from the list, so we add it to the string builder
                                            chunkAddition.Append(nt);
                                            continue;
                                        }
                                        else
                                        {
                                            quote2 = GetQuoteEscapeStatus(nt, nqI);
                                            if (quote2 == ChunkQuoteEnum.Terminator)
                                            {
                                                if (nt[nqI + 1] == ',' || nt[nqI + 1] == ':' || char.IsWhiteSpace(nt[nqI + 1]) || nt[nqI + 1] == '}')
                                                {
                                                    // excellent.  this terminates our string
                                                    // add the substring of the beginning of this chunk up to the character after the double quote to the string builder
                                                    chunkAddition.Append(nt.Substring(0, nqI + 1));

                                                    // hard update the chunk we just pulled a substring from
                                                    chunks[sindex] = new Tuple<string, int>(nt.Substring(nqI + 1), chunks[sindex].Item2);

                                                    // remove all of the chunks between the modified one and the one we started at
                                                    chunks = chunks.Where(tpl => tpl.Item2 <= cindex || tpl.Item2 >= sindex).ToArray();

                                                    // append the string builder's content to the end of the current chunk
                                                    chunks[cindex] = new Tuple<string, int>(chunks[cindex].Item1 + chunkAddition.ToString(), chunks[cindex].Item2);

                                                    found = true;
                                                } 
                                                else
                                                {
                                                    // opening double quote within a string.  this is malformed json
                                                    Outcome.Add(new LogItemBase(-1, -1, $"Invalid position for a double quote '\"'.  Malformed json provided!"));
                                                    break;
                                                }
                                            }
                                            else
                                            {
                                                // keep going
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // we don't have all of the json
                                        Outcome.Add(new LogItemBase(-1, -1, $"Incomplete json string provided.  Malformed json provided!"));
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            // if we don't have it, then this is likely the start of a string found in the next chunk.
                            // Move this character to the next chunk at the beginning
                            if (chunks.Length > (cindex + 1))
                            {
                                // append the last character from this chunk to the beginning of the next chunk
                                chunks[cindex + 1] = new Tuple<string, int>(t[t.Length - 1].ToString() + chunks[cindex + 1].Item1, chunks[cindex + 1].Item2);

                                // remove the quote from the end of this chunk
                                chunks[cindex] = new Tuple<string, int>(t.Substring(0, t.Length - 1), chunks[cindex].Item2);
                            } 
                            else
                            {
                                // we don't have all of the json
                                Outcome.Add(new LogItemBase(-1, -1, $"Incomplete json string provided.  Malformed json provided!"));
                                break;
                            }
                        }
                    }
                    else
                    {
                        // no valid quote found, nothing to do here
                    }
                }
            }

            // Ensure that all tokens are confined to a single chunk
            // (this is done after strings because it must be known if the text is in a string)
            // tokens in strings are ignored, after all
            cindex = -1;
            var boolCharSets = new Tuple<string, string>("tru", "fals");

            while (true)
            {
                cindex++;
                if (cindex >= chunks.Length)
                {
                    break;
                }

                string t = chunks[cindex].Item1;
                char tLast = t[t.Length - 1];

                // check the last character in the chunk
                if (char.IsNumber(tLast) || tLast == '.') // is it a number?
                {
                    if (cindex + 1 >= chunks.Length)
                    {
                        break;
                    }

                    // ensure we have the rest of the number in this chunk
                    // we are looking for a non-number token defining character (not a digit or period '.')
                    bool done = false;
                    StringBuilder chunkAddition = new StringBuilder();
                    int nindex = cindex + 1; // start on the next chunk
                    int completeChunkRemoval = 0;
                    while (!done)
                    {
                        string nt = chunks[nindex].Item1;
                        int nchunksubindex = 0;

                        if (char.IsNumber(nt[nchunksubindex]) || nt[nchunksubindex] == '.')
                        {
                            // if it is number defining, then we need to gather it and any following characters of those kinds and store them
                            while (true)
                            {
                                if (char.IsNumber(nt[nchunksubindex]) || nt[nchunksubindex] == '.')
                                {
                                    chunkAddition.Append(nt[nchunksubindex]);
                                    nchunksubindex++;

                                    if (nt.Length < nchunksubindex)
                                    {
                                        nindex++;
                                        completeChunkRemoval++;
                                        break;
                                    }
                                }
                                else
                                {
                                    done = true;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            // if it is not a number / period, we are done
                            break;
                        }
                    }

                    if (chunkAddition.Length > 0)
                    {
                        // we now have all of the number
                        // first remove all of the complete chunks to be removed
                        chunks = chunks.Where(tpl => tpl.Item2 <= cindex || tpl.Item2 >= nindex).ToArray();

                        // now remove the number of characters in the chunkAddition from the nindex chunk
                        chunks[nindex] = new Tuple<string, int>(chunks[nindex].Item1.Substring(chunkAddition.Length), chunks[nindex].Item2);

                        // now add the addition to the end of the current chunk
                        chunks[cindex] = new Tuple<string, int>(chunks[cindex].Item1 + chunkAddition.ToString(), chunks[cindex].Item2);
                    }
                }
                else if (
                    boolCharSets.Item1.ToCharArray().Contains(char.ToLower(tLast)) ||
                    boolCharSets.Item2.ToCharArray().Contains(char.ToLower(tLast))) // check if it's in true and then check false
                {
                    if (cindex + 1 >= chunks.Length)
                    {
                        break;
                    }

                    // we only have work to do if the character at the end is t, r, u in the case of true, or f, a, l, s in the case of false
                    // if we find an e, that is a complete token

                    // it's beautiful that true and false end with the same letter, but do not share any others
                    // this means a switch can fall through for each one cleanly
                    bool positive = false;
                    switch (char.ToLower(tLast))
                    {
                        case 't':
                        case 'r':
                        case 'u':
                            positive = true; // looking for true
                            break;
                        case 'f':
                        case 'a':
                        case 'l':
                        case 's':
                            positive = false; // looking for false
                            break;
                    }

                    // loop through the characters until we find an e
                    // move everything to the current chunk and we're done
                    // no need to worry about chunk bridging (a token containing an entire chunk and then some)
                    bool done = false;
                    StringBuilder chunkAddition = new StringBuilder();
                    int bindex = cindex + 1; // start on the next chunk
                    string nt = chunks[bindex].Item1;
                    int nchunksubindex = 0;

                    while (!done)
                    {

                        if ((boolCharSets.Item1.ToCharArray().Contains(char.ToLower(nt[nchunksubindex])) && positive) ||
                            (boolCharSets.Item2.ToCharArray().Contains(char.ToLower(nt[nchunksubindex])) && !positive))
                        {
                            // if it is part of a boolean token then get the rest
                            chunkAddition.Append(nt[nchunksubindex]);
                            nchunksubindex++;

                            if (nt.Length < nchunksubindex)
                            {
                                Outcome.Add(new LogItemBase(-1, -1, $"Bad boolean token.  Malformed json provided!"));
                                done = true;
                                break;
                            }
                            else if (char.ToLower(nt[nchunksubindex]) == 'e')
                            {
                                done = true;
                            }
                        }
                        else
                        {
                            done = true;
                        }
                    }

                    if (chunkAddition.Length > 0)
                    {
                        // we now have all of the boolean token
                        // now remove the number of characters in the chunkAddition from the nindex chunk
                        chunks[bindex] = new Tuple<string, int>(chunks[bindex].Item1.Substring(chunkAddition.Length), chunks[bindex].Item2);

                        // now add the addition to the end of the current chunk
                        chunks[cindex] = new Tuple<string, int>(chunks[cindex].Item1 + chunkAddition.ToString(), chunks[cindex].Item2);
                    }
                }
            }

            return chunks;
        }

        /// <summary>
        /// Determines if a double quote at a given index is escaped by checking the preceding characters
        /// </summary>
        /// <param name="chunk">the text to check in</param>
        /// <param name="index">the double quote's index</param>
        /// <returns>an enumerable value indicating the result</returns>
        ChunkQuoteEnum GetQuoteEscapeStatus(string chunk, int index)
        {
            var quote = ChunkQuoteEnum.Unknown;

            if (index > 1 && (chunk[index - 1] == '\\' && chunk[index - 2] != '\\'))
            {
                // escaped double quote
                quote = ChunkQuoteEnum.Escaped;
            }
            else if (index > 1 && (chunk[index - 1] == '\\' && chunk[index - 2] == '\\'))
            {
                // escaped slash preceding a double quote (valid double quote)
                quote = ChunkQuoteEnum.Valid;
            }
            // if we only have a single preceding character then check to see if it is an escaped or valid double quote
            else if (index > 0 && (chunk[index - 1] != '\\'))
            {
                // valid double quote
                quote = ChunkQuoteEnum.Valid;
            }
            else if (index > 0 && (chunk[index - 1] == '\\'))
            {
                // escaped double quote
                quote = ChunkQuoteEnum.Escaped;
            }
            else
            {
                // if we don't have the character before it,
                // we can assume that the previous chunk didn't find a reason to grab this double quote.
                // continue to part 2 (do nothing here)
                quote = ChunkQuoteEnum.Valid;
            }

            // two approaches to checking for terminators
            // 1) does the quote have a string follower character after it (if it is not at the end of the string)?
            if (quote == ChunkQuoteEnum.Valid && chunk.Length > index + 1)
            {
                if (chunk[index + 1] == ',' || chunk[index + 1] == ':' || char.IsWhiteSpace(chunk[index + 1]) || chunk[index + 1] == '}')
                {
                    quote = ChunkQuoteEnum.Terminator;
                }
            }
            // 2) is the first non-escaped quote preceding the target quote an opener?
            else if (quote == ChunkQuoteEnum.Valid && chunk.Length - 1 == index)
            {
                var qt = ChunkQuoteEnum.Unknown;
                var nextIndex = index;
                while (true)
                {
                    nextIndex = chunk.Substring(0, chunk.Length - (chunk.Length - nextIndex)).LastIndexOf("\"");
                    qt = GetQuoteEscapeStatus(chunk, nextIndex);

                    // note that we ignore escaped ones completely and keep going
                    if (qt == ChunkQuoteEnum.Valid)
                    {
                        // this means that ours is a terminator to this one
                        quote = ChunkQuoteEnum.Terminator;
                        break;
                    }
                    else if (qt == ChunkQuoteEnum.Terminator)
                    {
                        // ours is an opener
                        break;
                    }
                }
            }

            return quote;
        }
    }
}
