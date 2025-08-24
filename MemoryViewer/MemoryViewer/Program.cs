using System;
using System.CommandLine;
using System.Linq;
using SDL3;
using Shared;

namespace MemoryViewer
{
	class Program
	{
		const int RESX = 320, RESY = 200;
		const int DOS_CONV = 640 * 1024;
		const int EMS = 64000;
		const int EMS_ADDRESS = (1024 + 192) * 1024;
		const int SCREENS = (DOS_CONV + EMS + RESX * RESY - 1) / (RESX * RESY) + 1; //round up + one extra screen for left over between columns

		//static readonly bool[] needRefresh = new bool[SCREENS];
		//static bool mustClearScreen = true;

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
		const string windowText = "AITD memory viewer";
		static string windowTitle, lastWindowTitle;

		static int Main(string[] args)
		{
			var width = new Option<int>("-width") { DefaultValueFactory = x => 640 };
			var height = new Option<int>("-height") { DefaultValueFactory = x => 480 };
			var zoom = new Option<int>("-zoom") { DefaultValueFactory = x => 2 };
			var rootCommand = new RootCommand() { width, height, zoom };

			rootCommand.SetAction(result =>
			{
				Program.width = result.GetValue(width);
				Program.height = result.GetValue(height);
				Program.zoom = result.GetValue(zoom);
				Run();
			});

			var parseResult = rootCommand.Parse(args);
			return parseResult.Invoke();
		}

		static int Run()
		{
			if (!SDL.Init(SDL.InitFlags.Video))
			{
				SDL.LogError(SDL.LogCategory.Application, $"SDL.Init() fail: {SDL.GetError()}\n");
				return 1;
			}

			var window = SDL.CreateWindow(windowTitle, width, height, SDL.WindowFlags.Resizable | SDL.WindowFlags.OpenGL);
			if (window == IntPtr.Zero)
			{
				SDL.LogError(SDL.LogCategory.Application, $"Window creation fail: {SDL.GetError()}\n");
				return 0;
			}

			var renderer = SDL.CreateRenderer(window, null);
			if (renderer == IntPtr.Zero)
			{
				SDL.LogError(SDL.LogCategory.Application, $"Renderer creation fail: {SDL.GetError()}\n");
				return 0;
			}

			SDL.SetRenderVSync(renderer, 1);

			IntPtr texture = SDL.CreateTexture(renderer, SDL.PixelFormat.ARGB8888, SDL.TextureAccess.Streaming, RESX, RESY);
			SDL.SetTextureBlendMode(texture, SDL.BlendMode.Blend);
			SDL.SetTextureScaleMode(texture, SDL.ScaleMode.Nearest);

			IntPtr paletteTexture = SDL.CreateTexture(renderer, SDL.PixelFormat.ARGB8888, SDL.TextureAccess.Streaming, 16, 16);
			SDL.SetTextureScaleMode(paletteTexture, SDL.ScaleMode.Nearest);
			UpdateTitle();
			//SetRefreshState(true);

			bool quit = false, mcb = false, minimized = false, showPalette = false;
			long paletteAddress = -1;

			ProcessMemory process = null;
			ulong lastCheck = 0, lastCheckPalette = 0;

			while (!quit)
			{
				while (SDL.PollEvent(out SDL.Event sdlEvent) && !quit)
				{
					switch ((SDL.EventType)sdlEvent.Type)
					{
						case SDL.EventType.Quit:
							quit = true;
							break;

						case SDL.EventType.MouseMotion:
						case SDL.EventType.MouseButtonDown:
							if (sdlEvent.Motion.State == SDL.MouseButtonFlags.Left)
							{
								int px = (int)sdlEvent.Motion.X / zoom;
								int py = (int)sdlEvent.Motion.Y / zoom;
								int palX = px / 20;
								int palY = py / 20;

								if (showPalette && palX < 16 && palY < 16)
								{
									int index = palX + palY * 16;
									windowTitle = $"{index} - 0x{palette[index] & 0xFFFFFF:X6}";
								}
								else
								{
									int page = RESX * (height / zoom);
									int mousePosition = (px % RESX) + (py * RESX) + (px / RESX * page) + (offset * DOS_CONV);

									int address = mousePosition - offset * DOS_CONV;
									if (address >= 0 && address < pixelData.Length && address < (DOS_CONV + EMS))
									{
										if (address >= DOS_CONV) mousePosition = address - DOS_CONV + EMS_ADDRESS;
										windowTitle = $"{mousePosition:X} - 0x{pixelData[address]:X2} ({pixelData[address]})";
									}
									else
									{
										UpdateTitle();
									}
								}

							}
							else if (sdlEvent.Motion.State == SDL.MouseButtonFlags.Right)
							{
								UpdateTitle();
							}
							break;

						case SDL.EventType.MouseWheel:
							if ((SDL.GetModState() & SDL.Keymod.Ctrl) != 0)
							{
								if (sdlEvent.Wheel.Y > 0)
								{
									SetZoom(zoom + 1);
								}
								else if (sdlEvent.Wheel.Y < 0)
								{
									SetZoom(zoom - 1);
								}
							}
							else
							{
								if (sdlEvent.Wheel.Y > 0)
								{
									SetOffset(offset - 1);
									UpdateTitle();
								}
								else if (sdlEvent.Wheel.Y < 0)
								{
									SetOffset(offset + 1);
									UpdateTitle();
								}
							}
							break;

						case SDL.EventType.KeyDown:
							bool control = (sdlEvent.Key.Mod & SDL.Keymod.Ctrl) != 0;
							switch (sdlEvent.Key.Key)
							{
								case SDL.Keycode.Space:
									mcb = !mcb;
									//SetRefreshState(true);
									break;

								case SDL.Keycode.Pagedown:
									SetOffset(offset + 1);
									UpdateTitle();
									break;

								case SDL.Keycode.Pageup:
									SetOffset(offset - 1);
									UpdateTitle();
									break;

								case SDL.Keycode.P:
									showPalette = !showPalette;
									UpdateTitle();
									//SetRefreshState(true);
									break;

								case SDL.Keycode.Equals:
								case SDL.Keycode.KpPlus:
									if (control)
									{
										SetZoom(zoom + 1);
									}
									break;

								case SDL.Keycode.Minus:
								case SDL.Keycode.KpMinus:
									if (control)
									{
										SetZoom(zoom - 1);
									}
									break;

								case SDL.Keycode.Alpha0:
								case SDL.Keycode.Kp0:
									if (control)
									{
										SetZoom(2);
									}
									break;
							}
							break;

						case SDL.EventType.WindowResized:
							width = sdlEvent.Window.Data1;
							height = sdlEvent.Window.Data2;
							//SetRefreshState(true);
							//mustClearScreen = true;
							break;

						case SDL.EventType.WindowMinimized:
							minimized = true;
							break;

						case SDL.EventType.WindowMaximized:
							minimized = false;
							break;
					}
				}

				if (process == null)
				{
					ulong time = SDL.GetTicks();
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
						ulong time = SDL.GetTicks();
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
						process.Read(pixelData, EMS_ADDRESS, EMS, DOS_CONV) > 0))
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
					//if (mustClearScreen)
					{
						SDL.SetRenderDrawColor(renderer, 30, 30, 30, 255);
						SDL.RenderClear(renderer);
					}

					int tm = (width + RESX * zoom - 1) / (RESX * zoom);
					int tn = (height + RESY * zoom - 1) / (RESY * zoom);

					Render(renderer, texture, tm, tn, pixels);

					if (mcb && offset == 0)
					{
						Render(renderer, texture, tm, tn, mcbPixels);
					}

					if (showPalette)
					{
						RenderPalette(renderer, paletteTexture);
					}

					SDL.RenderPresent(renderer);

					//SetRefreshState(false);
					//mustClearScreen = false;
				}
				else
				{
					SDL.Delay(1);
				}

				if (windowTitle != lastWindowTitle)
				{
					SDL.SetWindowTitle(window, $"{windowText} - {windowTitle}");
					lastWindowTitle = windowTitle;
				}

				//swap buffers
				(oldPixelData, pixelData) = (pixelData, oldPixelData);
			}

			SDL.DestroyTexture(texture);
			SDL.DestroyRenderer(renderer);
			SDL.DestroyWindow(window);
			SDL.Quit();

			return 0;

			void UpdateTitle()
			{
				windowTitle = $"{offset * DOS_CONV:X}:{(offset + 1) * DOS_CONV:X}";
			}
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
					//bool refresh = false;
					for (int i = 0 ; i < RESX * RESY ; i += 16)
					{
						if (*pixelsPtr != *oldPixelsPtr || *(pixelsPtr+1) != *(oldPixelsPtr+1))
						{
							for (int j = start ; j < start + 16 ; j++)
							{
								pixels[j] = palette[pixelData[j]];
							}

							//refresh = true;
						}
						else if (needPaletteUpdate)
						{
							for (int j = start ; j < start + 16 ; j++)
							{
								byte color = pixelData[j];
								if (needPaletteUpdate256[color] && j < (EMS + DOS_CONV))
								{
									pixels[j] = palette[color];
									//refresh = true;
								}
							}
						}

						start += 16;
						pixelsPtr += 2;
						oldPixelsPtr += 2;
					}

					//needRefresh[k] |= refresh;
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

				//SetRefreshState(true);
			}
		}

		static unsafe void Render(IntPtr renderer, IntPtr texture, int tm, int tn, uint[] source)
		{
			var textureRect = new SDL.Rect { X = 0, Y = 0, W = RESX, H = RESY };
			var textureRectF = new SDL.FRect { X = 0, Y = 0, W = RESX, H = RESY };

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

					//if (needRefresh[index] || needRefresh[nextIndex])
					{
						fixed (uint* pixelsBuf = &source[position])
						{
							SDL.UpdateTexture(texture, in textureRect, (IntPtr)pixelsBuf, RESX * sizeof(uint));
						}

						var drawRect = new SDL.FRect { X = m * RESX * zoom, Y = n * RESY * zoom, W = RESX * zoom, H = RESY * zoom };

						SDL.RenderTexture(renderer, texture, in textureRectF, in drawRect);
					}
				}

				skip += RESX * (height / zoom);
			}
		}

		static unsafe void RenderPalette(IntPtr renderer, IntPtr texture)
		{
			var textureRect = new SDL.Rect{ X = 0, Y = 0, W = 16, H = 16 };
			var textureRectF = new SDL.FRect { X = 0, Y = 0, W = 16, H = 16 };
			var drawRect = new SDL.FRect { X = 0, Y = 0, W = RESX * zoom, H = RESX * zoom };

			fixed (uint* pixelsBuf = &palette[0])
			{
				SDL.UpdateTexture(texture, in textureRect, (IntPtr)pixelsBuf, 16 * sizeof(uint));
			}

			SDL.RenderTexture(renderer, texture, in textureRectF, in drawRect);
		}

		//static void SetRefreshState(bool state)
		//{
		//	for (int i = 0 ; i < SCREENS ; i++)
		//	{
		//		needRefresh[i] = state;
		//	}
		//}

		static void ClearAll()
		{
			Array.Clear(pixelData, 0, pixelData.Length);
			Array.Clear(oldPixelData, 0, oldPixelData.Length);
			Array.Clear(pixels, 0, pixels.Length);
			Array.Clear(mcbPixels, 0, mcbPixels.Length);
			//SetRefreshState(true);
		}

		static void SetZoom(int newZoom)
		{
			if (newZoom != zoom && newZoom >= 1 && newZoom <= 8)
			{
				zoom = newZoom;
				//mustClearScreen = true;
				//SetRefreshState(true);
			}
		}

		static void SetOffset(int newOffset)
		{
			const int MAXOFFSET = (16 * 1024 * 1024 - DOS_CONV) / DOS_CONV; //16MB

			if (offset != newOffset && newOffset >= 0 && newOffset < MAXOFFSET)
			{
				offset = newOffset;
				//SetRefreshState(true);
			}
		}
	}
}