using System;
using System.Collections.Generic;
using System.Linq;
using SDL2;
using Shared;

namespace MemoryViewer
{
	class Program
	{
		const int RESX = 320, RESY = 60;
		const int DOS_CONV = 640 * 1024;
		const int EMS = 64000;
		const int SCREENS = (DOS_CONV + EMS + RESX * RESY - 1) / (RESX * RESY) + 1; //round up + one extra screen for left over between columns

		static readonly bool[] needRefresh = new bool[SCREENS];
		static bool mustClearScreen = true;
		static bool needPaletteUpdate;
		static readonly bool[] needPaletteUpdate256 = new bool[256];
		static int offset;

		static byte[] pixelData = new byte[RESX * RESY * SCREENS];
		static byte[] oldPixelData = new byte[RESX * RESY * SCREENS];
		static readonly uint[] pixels = new uint[RESX * RESY * SCREENS];
		static readonly uint[] mcbPixels = new uint[RESX * RESY * SCREENS];

		static readonly uint[] palette = new uint[256];
		static readonly byte[] palette256 = new byte[768];

		static int width, height, zoom;

		static int Main(string[] args)
		{
			return (int)CommandLine.ParseAndInvoke(args, new Func<int, int, int, int>(Run));
		}

		static int Run(int width = 640, int height = 480, int zoom = 2)
		{
			Program.width = width;
			Program.height = height;
			Program.zoom = zoom;
			return Run();
		}

		static int Run()
		{
			//init SDL
			SDL.SDL_Init(SDL.SDL_INIT_VIDEO);

			IntPtr window = SDL.SDL_CreateWindow("AITD memory viewer", SDL.SDL_WINDOWPOS_UNDEFINED, SDL.SDL_WINDOWPOS_UNDEFINED, width, height, SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE);
			IntPtr renderer = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED | SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);
			//IntPtr renderer = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_SOFTWARE);

			//SDL.SDL_SetHint(SDL.SDL_HINT_RENDER_SCALE_QUALITY, "linear");
			IntPtr texture = SDL.SDL_CreateTexture(renderer, SDL.SDL_PIXELFORMAT_ARGB8888, (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING, RESX, RESY);
			SDL.SDL_SetTextureBlendMode(texture, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);

			IntPtr paletteTexture = SDL.SDL_CreateTexture(renderer, SDL.SDL_PIXELFORMAT_ARGB8888, (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING, 16, 16);

			SetRefreshState(true);

			bool quit = false, mcb = false, minimized = false, showPalette = false;
			long paletteAddress = -1;

			ProcessMemory process = null;
			uint lastCheck = 0, lastCheckPalette = 0;

			while (!quit)
			{
				while (SDL.SDL_PollEvent(out SDL.SDL_Event sdlEvent) != 0 && !quit)
				{
					switch (sdlEvent.type)
					{
						case SDL.SDL_EventType.SDL_QUIT:
							quit = true;
							break;

						case SDL.SDL_EventType.SDL_MOUSEWHEEL:
							if ((SDL.SDL_GetModState() & SDL.SDL_Keymod.KMOD_CTRL) != 0)
							{
								if (sdlEvent.wheel.y > 0)
								{
									SetZoom(zoom + 1);
								}
								else if (sdlEvent.wheel.y < 0)
								{
									SetZoom(zoom - 1);
								}
							}
							else
							{
								if (sdlEvent.wheel.y > 0)
								{
									SetOffset(offset - 1);
								}
								else if (sdlEvent.wheel.y < 0)
								{
									SetOffset(offset + 1);
								}
							}
							break;

						case SDL.SDL_EventType.SDL_KEYDOWN:
							bool control = (sdlEvent.key.keysym.mod & SDL.SDL_Keymod.KMOD_CTRL) != 0;
							switch (sdlEvent.key.keysym.sym)
							{
								case SDL.SDL_Keycode.SDLK_SPACE:
									mcb = !mcb;
									SetRefreshState(true);
									break;

								case SDL.SDL_Keycode.SDLK_PAGEDOWN:
									SetOffset(offset + 1);
									break;

								case SDL.SDL_Keycode.SDLK_PAGEUP:
									SetOffset(offset - 1);
									break;

								case SDL.SDL_Keycode.SDLK_p:
									showPalette = !showPalette;
									SetRefreshState(true);
									break;

								case SDL.SDL_Keycode.SDLK_EQUALS:
								case SDL.SDL_Keycode.SDLK_KP_PLUS:
									if (control)
									{
										SetZoom(zoom + 1);
									}
									break;

								case SDL.SDL_Keycode.SDLK_MINUS:
								case SDL.SDL_Keycode.SDLK_KP_MINUS:
									if (control)
									{
										SetZoom(zoom - 1);
									}
									break;

								case SDL.SDL_Keycode.SDLK_0:
								case SDL.SDL_Keycode.SDLK_KP_0:
									if (control)
									{
										SetZoom(2);
									}
									break;
							}
							break;

						case SDL.SDL_EventType.SDL_WINDOWEVENT:
							switch (sdlEvent.window.windowEvent)
							{
								case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_RESIZED:
									width = sdlEvent.window.data1;
									height = sdlEvent.window.data2;
									SetRefreshState(true);
									mustClearScreen = true;
									break;

								case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_MINIMIZED:
									minimized = true;
									break;

								case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_MAXIMIZED:
									minimized = false;
									break;
							}
							break;
					}
				}

				if (process == null)
				{
					uint time = SDL.SDL_GetTicks();
					if ((time - lastCheck) > 1000 || lastCheck == 0)
					{
						lastCheck = time;
						paletteAddress = -1;

						process = DosBox.SearchDosBox(false);
					}
				}

				if (process != null)
				{
					if (paletteAddress == -1)
					{
						uint time = SDL.SDL_GetTicks();
						if ((time - lastCheckPalette) > 1000 || lastCheckPalette == 0)
						{
							lastCheckPalette = time;

							//3 bytes + EGA 16 colors palette (see DOSBox VGA_Dac in vga.h)
							var pattern = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f };
							paletteAddress = process.SearchForBytePattern(buffer => Tools.IndexOf(buffer, pattern, 1, 4));
							if (paletteAddress != -1)
							{
								paletteAddress += pattern.Length;
							}
						}
					}
				}

				if (process != null)
				{
					//DOS conventional memory (640KB)
					//EMS memory (64000B) (skip 64KB (HMA) + 128KB (VCPI))
					if (!(process.Read(pixelData, offset * DOS_CONV, DOS_CONV) > 0 &&
						process.Read(pixelData, (1024 + 192) * 1024, EMS, DOS_CONV) > 0))
					{
						process.Close();
						process = null;
					}
				}

				if (process != null)
				{
					if (paletteAddress != -1)
					{
						if (process.Read(palette256, paletteAddress, palette256.Length) <= 0)
						{
							process.Close();
							process = null;
						}
					}
				}

				UpdatePalette();
				Update();
				if (offset == 0)
				{
					UpdateMCB();
				}

				if (!minimized)
				{
					//render
					if (mustClearScreen)
					{
						SDL.SDL_SetRenderDrawColor(renderer, 30, 30, 30, 255);
						SDL.SDL_RenderClear(renderer);
					}

					int tm = (width + RESX * zoom - 1) / (RESX * zoom);
					int tn = (height + RESY * zoom - 1) / (RESY * zoom);

					Render(renderer, texture, tm, tn, pixels);

					if (mcb && offset == 0)
					{
						Render(renderer, texture, tm, tn, mcbPixels);
					}

					if (showPalette && offset == 0)
					{
						RenderPalette(renderer, paletteTexture);
					}

					SDL.SDL_RenderPresent(renderer);

					SetRefreshState(false);
					mustClearScreen = false;
				}
				else
				{
					SDL.SDL_Delay(1);
				}

				//swap buffers
				(oldPixelData, pixelData) = (pixelData, oldPixelData);
			}

			SDL.SDL_DestroyTexture(texture);
			SDL.SDL_DestroyRenderer(renderer);
			SDL.SDL_DestroyWindow(window);
			SDL.SDL_Quit();

			return 0;
		}

		static void UpdatePalette()
		{
			needPaletteUpdate = false;

			int src = 0;
			for (int i = 0 ; i < 256 ; i++)
			{
				var r = palette256[src++];
				var g = palette256[src++];
				var b = palette256[src++];

				uint val = unchecked((uint)((255 << 24) | ((r << 2) | (r >> 4)) << 16 | ((g << 2) | (g >> 4)) << 8 | ((b << 2) | (b >> 4)) << 0));
				bool diff = palette[i] != val;
				needPaletteUpdate256[i] = diff;

				if (diff)
				{
					palette[i] = val;
					needPaletteUpdate = true;
				}
			}
		}

		static unsafe void Update()
		{
			//compare current vs old and only update pixels that have changed
			fixed (byte* pixelsBytePtr = pixelData)
			fixed (byte* oldPixelsBytePtr = oldPixelData)
			{
				ulong* pixelsPtr = (ulong*)pixelsBytePtr;
				ulong* oldPixelsPtr = (ulong*)oldPixelsBytePtr;
				int start = 0;

				for (int k = 0 ; k < SCREENS ; k++)
				{
					bool refresh = false;
					for (int i = 0 ; i < RESX * RESY ; i += 16)
					{
						if (*pixelsPtr != *oldPixelsPtr || *(pixelsPtr+1) != *(oldPixelsPtr+1))
						{
							for (int j = start ; j < start + 16 ; j++)
							{
								pixels[j] = palette[pixelData[j]];
							}

							refresh = true;
						}
						else if (needPaletteUpdate)
						{
							for (int j = start ; j < start + 16 ; j++)
							{
								byte color = pixelData[j];
								if (needPaletteUpdate256[color] && j < (EMS + DOS_CONV))
								{
									pixels[j] = palette[color];
									refresh = true;
								}
							}
						}

						start += 16;
						pixelsPtr += 2;
						oldPixelsPtr += 2;
					}

					needRefresh[k] |= refresh;
				}
			}
		}

		static void UpdateMCB()
		{
			if (!DosMCB.GetMCBs(pixelData).SequenceEqual(DosMCB.GetMCBs(oldPixelData)))
			{
				//clear old MCB
				foreach (var block in DosMCB.GetMCBs(oldPixelData))
				{
					int dest = block.Position - 16;
					int length = Math.Min(block.Size + 16, mcbPixels.Length - dest);
					Array.Clear(mcbPixels, dest, length);
				}

				int psp = pixelData.ReadUnsignedShort(0x0B20 + 0x10) * 16; // dos swappable area (SDA) + 10h

				bool inverse = true;
				foreach (var block in DosMCB.GetMCBs(pixelData))
				{
					uint color;
					if (block.Owner == 0) color = 0x90008000; //free
					else if (block.Owner != psp) color = 0x90808000;
					else if (block.Position == psp) color = 0x90800000; //current executable
					else color = inverse ? 0x900080F0 : 0x902000A0; //used

					int dest = block.Position - 16;
					int length = Math.Min(16, mcbPixels.Length - dest);
					for (int i = 0 ; i < length ; i++)
					{
						mcbPixels[dest++] = 0x90FF00FF;
					}

					length = Math.Min(block.Size, mcbPixels.Length - dest);
					for (int i = 0 ; i < length ; i++)
					{
						mcbPixels[dest++] = color;
					}

					inverse = !inverse;
				}

				SetRefreshState(true);
			}
		}

		static unsafe void Render(IntPtr renderer, IntPtr texture, int tm, int tn, uint[] source)
		{
			var textureRect = new SDL.SDL_Rect { x = 0, y = 0, w = RESX, h = RESY };

			int skip = 0;
			for (int m = 0; m < tm; m++)
			{
				for (int n = 0; n < tn; n++)
				{
					int position = skip + n * RESX * RESY;
					int nextPosition = position + RESX * RESY;

					if (nextPosition > source.Length)
					{
						break;
					}

					int index = position / (RESX * RESY);
					int nextIndex = (nextPosition - 1) / (RESX * RESY);

					if (needRefresh[index] || needRefresh[nextIndex])
					{
						fixed (uint* pixelsBuf = &source[position])
						{
							SDL.SDL_UpdateTexture(texture, ref textureRect, (IntPtr)pixelsBuf, RESX * sizeof(uint));
						}

						var drawRect = new SDL.SDL_Rect { x = m * RESX * zoom, y = n * RESY * zoom, w = RESX * zoom, h = RESY * zoom };

						SDL.SDL_RenderCopy(renderer, texture, ref textureRect, ref drawRect);
					}
				}

				skip += RESX * (height / zoom);
			}
		}

		static unsafe void RenderPalette(IntPtr renderer, IntPtr texture)
		{
			var textureRect = new SDL.SDL_Rect	{ x = 0, y = 0, w = 16, h = 16 };
			var drawRect = new SDL.SDL_Rect { x = 0, y = 0, w = RESX * zoom, h = RESX * zoom };

			fixed (uint* pixelsBuf = &palette[0])
			{
				SDL.SDL_UpdateTexture(texture, ref textureRect, (IntPtr)pixelsBuf, 16 * sizeof(uint));
			}

			SDL.SDL_RenderCopy(renderer, texture, ref textureRect, ref drawRect);
		}

		static void SetRefreshState(bool state)
		{
			for (int i = 0 ; i < SCREENS ; i++)
			{
				needRefresh[i] = state;
			}
		}

		static void ClearAll()
		{
			Array.Clear(pixelData, 0, pixelData.Length);
			Array.Clear(oldPixelData, 0, oldPixelData.Length);
			Array.Clear(pixels, 0, pixels.Length);
			Array.Clear(mcbPixels, 0, mcbPixels.Length);
			SetRefreshState(true);
		}

		static void SetZoom(int newZoom)
		{
			if (newZoom != zoom && newZoom >= 1 && newZoom <= 8)
			{
				zoom = newZoom;
				mustClearScreen = true;
				SetRefreshState(true);
			}
		}

		static void SetOffset(int newOffset)
		{
			const int MAXOFFSET = (16 * 1024 * 1024 - DOS_CONV) / DOS_CONV; //16MB

			if (offset != newOffset && newOffset >= 0 && newOffset < MAXOFFSET)
			{
				offset = newOffset;
				SetRefreshState(true);
			}
		}
	}
}