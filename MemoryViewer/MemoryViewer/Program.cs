using System;
using System.IO;
using System.Linq;
using System.Reflection;
using SDL2;
using Shared;

namespace MemoryViewer
{
	class Program
	{
		public static int Main(string[] args)
		{
			const int RESX = 320;
			const int RESY = 240;
			bool mcb = false;

			int winx = GetArgument(args, "-screen-width") ?? 640;
			int winy = GetArgument(args, "-screen-height") ?? 480;
			int zoom = GetArgument(args, "-zoom") ?? 2;

			//init SDL
			SDL.SDL_Init(SDL.SDL_INIT_VIDEO);

			IntPtr window = SDL.SDL_CreateWindow("AITD memory viewer", SDL.SDL_WINDOWPOS_UNDEFINED, SDL.SDL_WINDOWPOS_UNDEFINED, winx, winy, SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE);
			IntPtr renderer = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED | SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);
			//IntPtr renderer = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_SOFTWARE);

			//SDL.SDL_SetHint(SDL.SDL_HINT_RENDER_SCALE_QUALITY, "linear");
			SDL.SDL_Rect textureRect = new SDL.SDL_Rect { x = 0, y = 0, w = RESX, h = RESY };
			IntPtr texture = SDL.SDL_CreateTexture(renderer, SDL.SDL_PIXELFORMAT_ARGB8888, (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING, RESX, RESY);

			SDL.SDL_Rect drawRect = new SDL.SDL_Rect { x = 0, y = 0, w = RESX, h = RESY };

			uint[] pal256 = new uint[256];
			using (var stream = Assembly.GetExecutingAssembly()
				   .GetManifestResourceStream("MemoryViewer.palette.dat"))
			using (BinaryReader br = new BinaryReader(stream))
			{
				var palette = br.ReadBytes(768);
				for(int i = 0; i < 256; i++)
				{
					byte r = palette[i * 3 + 0];
					byte g = palette[i * 3 + 1];
					byte b = palette[i * 3 + 2];
					pal256[i] = (uint)(r << 16 | g << 8 | b);
				}
			}

			bool quit = false;
			uint[] pixels = new uint[RESX * RESY * 10];
			byte[] pixelData = new byte[640 * 1024 + 64000];
			byte[] oldPixelData = new byte[640 * 1024 + 64000];
			byte[] dosMemory = new byte[640 * 1024];

			ProcessMemoryReader processReader = null;
			long memoryAddress = -1;
			uint lastCheck = 0;

			while(!quit)
			{
				SDL.SDL_Event sdlEvent;
				while(SDL.SDL_PollEvent(out sdlEvent) != 0 && !quit)
				{
					switch(sdlEvent.type)
					{
						case SDL.SDL_EventType.SDL_QUIT:
							quit = true;
							break;

						case SDL.SDL_EventType.SDL_KEYDOWN:
							if(sdlEvent.key.keysym.sym == SDL.SDL_Keycode.SDLK_SPACE)
							{
								mcb = !mcb;
							}
							break;

						case SDL.SDL_EventType.SDL_WINDOWEVENT:

							if(sdlEvent.window.windowEvent == SDL.SDL_WindowEventID.SDL_WINDOWEVENT_RESIZED)
							{
								winx = sdlEvent.window.data1;
								winy = sdlEvent.window.data2;
							}
							break;
					}
				}

				if (processReader == null)
				{
					uint time = SDL.SDL_GetTicks();
					if ((time - lastCheck) > 1000 || lastCheck == 0)
					{
						lastCheck = time;

						int processId = DosBox.SearchProcess();
						if (processId != -1)
						{
							processReader = new ProcessMemoryReader(processId);
							memoryAddress = processReader.SearchFor16MRegion();
							if(memoryAddress == -1)
							{
								processReader.Close();
								processReader = null;
							}
						}
					}
				}

				if (processReader != null)
				{
					//EMS memory (64000B) (skip 64KB (HMA) + 128KB (VCPI))
					//DOS conventional memory (640KB)
					if(!(processReader.Read(pixelData, memoryAddress+(1024+192)*1024, 64000) > 0 &&
						(processReader.Read(dosMemory, memoryAddress, dosMemory.Length) > 0)))
					{
						processReader.Close();
						processReader = null;
					}
				}

				Array.Copy(dosMemory, 0, pixelData, 64000, dosMemory.Length);

				if (mcb)
				{
					DisplayMCB(dosMemory, pixelData);
				}

				unsafe
				{
					fixed (byte* pixelsBytePtr = pixelData)
					fixed (byte* oldPixelsBytePtr = oldPixelData)
					{
						ulong* pixelsPtr = (ulong*)pixelsBytePtr;
						ulong* oldPixelsPtr = (ulong*)oldPixelsBytePtr;

						for(int i = 0 ; i < pixelData.Length ; i += 8)
						{
							if(*pixelsPtr != *oldPixelsPtr)
							{
								for(int j = i ; j < i + 8 ; j++)
								{
									pixels[j] = pal256[pixelData[j]];
								}
							}

							pixelsPtr++;
							oldPixelsPtr++;
						}
					}
				}

				SDL.SDL_RenderClear(renderer);

				int tm = (winx + RESX * zoom - 1) / (RESX * zoom);
				int tn = (winy + RESY * zoom - 1) / (RESY * zoom);

				int skip = 0;
				for(int m = 0 ; m < tm ; m++)
				{
					for(int n = 0 ; n < tn ; n++)
					{
						int position = skip + n * RESX * RESY;
						if (position >= pixelData.Length) continue;

						unsafe
						{
							fixed (uint* pixelsBuf = &pixels[position])
							{
								SDL.SDL_UpdateTexture(texture, ref textureRect, (IntPtr)pixelsBuf, RESX * sizeof(uint));
							}
						}

						drawRect.x = m * RESX * zoom;
						drawRect.y = n * RESY * zoom;
						drawRect.w = RESX * zoom;
						drawRect.h = RESY * zoom;
						SDL.SDL_RenderCopy(renderer, texture, ref textureRect, ref drawRect);
					}

					skip += RESX * (winy / zoom);
				}

				SDL.SDL_RenderPresent(renderer);

				//swap buffers
				var tmp = pixelData;
				pixelData = oldPixelData;
				oldPixelData = tmp;
			}

			SDL.SDL_DestroyTexture(texture);
			SDL.SDL_DestroyRenderer(renderer);
			SDL.SDL_DestroyWindow(window);
			SDL.SDL_Quit();

			return 0;
		}

		static void DisplayMCB(byte[] dosMemory, byte[] pixelData)
		{
			bool inverse = true;
			foreach(var block in DosBox.GetMCBs(dosMemory))
			{
				int dest = 64000 + block.Position - 16;
				int length = Math.Min(block.Size + 16, pixelData.Length - dest);

				byte color = inverse ? (byte)189 : (byte)187;
				if(block.Owner == 0) color = 169; //free

				for (int i = 0 ; i < length ; i++)
				{
					pixelData[dest++] = color;
				}

				inverse = !inverse;
			}
		}

		public static int? GetArgument(string[] args, string name)
		{
			int index = Array.IndexOf(args, name);
			if (index >= 0 && index < (args.Length - 1))
			{
				int value;
				if(int.TryParse(args[index + 1], out value))
				{
					return value;
				}
			}

			return null;
		}
	}
}