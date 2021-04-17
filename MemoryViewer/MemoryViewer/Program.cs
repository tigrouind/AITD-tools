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
		const int RESX = 320;
		const int RESY = 240;
		const int SCREENS = 10;
		static int winx, winy, zoom;
		static readonly bool[] needRefresh = new bool[SCREENS];
			
		public static int Main(string[] args)
		{
			winx = Tools.GetArgument(args, "-screen-width") ?? 640;
			winy = Tools.GetArgument(args, "-screen-height") ?? 480;
			zoom = Tools.GetArgument(args, "-zoom") ?? 2;

			//init SDL
			SDL.SDL_Init(SDL.SDL_INIT_VIDEO);

			IntPtr window = SDL.SDL_CreateWindow("AITD memory viewer", SDL.SDL_WINDOWPOS_UNDEFINED, SDL.SDL_WINDOWPOS_UNDEFINED, winx, winy, SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE);
			IntPtr renderer = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED | SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);
			//IntPtr renderer = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_SOFTWARE);

			//SDL.SDL_SetHint(SDL.SDL_HINT_RENDER_SCALE_QUALITY, "linear");
			IntPtr texture = SDL.SDL_CreateTexture(renderer, SDL.SDL_PIXELFORMAT_ARGB8888, (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING, RESX, RESY);
			
			uint[] palette = LoadPalette();
			SetRefreshState(true);

			bool quit = false, mcb = false;
			uint[] pixels = new uint[RESX * RESY * SCREENS];
			uint[] mcbPixels = new uint[RESX * RESY * SCREENS];			
			byte[] pixelData = new byte[RESX * RESY * SCREENS];
			byte[] oldPixelData = new byte[RESX * RESY * SCREENS];			

			ProcessMemoryReader reader = null;
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
								SetRefreshState(true);
							}
							break;

						case SDL.SDL_EventType.SDL_WINDOWEVENT:

							if(sdlEvent.window.windowEvent == SDL.SDL_WindowEventID.SDL_WINDOWEVENT_RESIZED)
							{
								winx = sdlEvent.window.data1;
								winy = sdlEvent.window.data2;
								SetRefreshState(true);
							}
							break;
					}
				}

				if (reader == null)
				{
					uint time = SDL.SDL_GetTicks();
					if ((time - lastCheck) > 1000 || lastCheck == 0)
					{
						lastCheck = time;

						int processId = DosBox.SearchProcess();
						if (processId != -1)
						{
							reader = new ProcessMemoryReader(processId);
							reader.BaseAddress = reader.SearchFor16MRegion();
							if(reader.BaseAddress == -1)
							{
								reader.Close();
								reader = null;
							}
						}
					}
				}

				if (reader != null)
				{
					//DOS conventional memory (640KB)
					//EMS memory (64000B) (skip 64KB (HMA) + 128KB (VCPI))					
					if(!(reader.Read(pixelData, 0, 640 * 1024) > 0 &&
						 reader.Read(pixelData, (1024+192)*1024, 64000, 640 * 1024) > 0))
					{
						reader.Close();
						reader = null;
					}
				}

				Update(pixelData, oldPixelData, pixels, palette);
				UpdateMCB(pixelData, oldPixelData, mcbPixels);

				//render
				int tm = (winx + RESX * zoom - 1) / (RESX * zoom);
				int tn = (winy + RESY * zoom - 1) / (RESY * zoom);
				
				SDL.SDL_SetTextureBlendMode(texture, SDL.SDL_BlendMode.SDL_BLENDMODE_NONE);
				Render(renderer, texture, tm, tn, pixels);
				
				if (mcb)
				{
					SDL.SDL_SetTextureBlendMode(texture, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
					Render(renderer, texture, tm, tn, mcbPixels);
				}

				SDL.SDL_RenderPresent(renderer);					
				SetRefreshState(false);

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
		
		static unsafe void Update(byte[] pixelData, byte[] oldPixelData, uint[] pixels, uint[] palette)
		{
			//compare current vs old and only update pixels that have changed
			fixed (byte* pixelsBytePtr = pixelData)
			fixed (byte* oldPixelsBytePtr = oldPixelData)
			{
				ulong* pixelsPtr = (ulong*)pixelsBytePtr;
				ulong* oldPixelsPtr = (ulong*)oldPixelsBytePtr;
				int start = 0;	
				
				for(int k = 0 ; k < SCREENS ; k++)
				{
					bool refresh = false;
					for(int i = 0 ; i < RESX * RESY ; i += 16)
					{					
						if(*pixelsPtr != *oldPixelsPtr || *(pixelsPtr+1) != *(oldPixelsPtr+1))
						{
							for(int j = start ; j < start + 16 ; j++)
							{
								pixels[j] = palette[pixelData[j]];
							}	
							
							refresh = true;
						}
						
						start += 16;
						pixelsPtr += 2;
						oldPixelsPtr += 2;
					}
					
					needRefresh[k] |= refresh;
				}
			}
		}
		
		static void UpdateMCB(byte[] pixelData, byte[] oldPixelData, uint[] pixels)
		{		
			if (!DosBox.GetMCBs(pixelData).SequenceEqual(DosBox.GetMCBs(oldPixelData)))
			{	
				//clear old MCB
				foreach (var block in DosBox.GetMCBs(oldPixelData))
				{
					int dest = block.Position - 16;
					int length = Math.Min(block.Size + 16, pixels.Length - dest);
					Array.Clear(pixels, dest, length);
				}
				
				int psp = pixelData.ReadUnsignedShort(0x0B30) * 16;
				
				bool inverse = true;
				foreach (var block in DosBox.GetMCBs(pixelData))
				{
					uint color;					
					if (block.Owner == 0) color = 0x90008000; //free
					else if (block.Owner != psp) color = 0x90808000; 
					else if (block.Position == psp) color = 0x90800000; //current executable
					else color = inverse ? 0x900080F0 : 0x902000A0; //used					
					
					int dest = block.Position - 16;					
					int length = Math.Min(16, pixels.Length - dest);					
					for (int i = 0 ; i < length ; i++)
					{
						pixels[dest++] = 0x90FF00FF;
					}
	
					length = Math.Min(block.Size, pixels.Length - dest);
					for (int i = 0 ; i < length ; i++)
					{
						pixels[dest++] = color;
					}
					
					inverse = !inverse;
				}
				
				SetRefreshState(true);
			}
		}
		
		static unsafe void Render(IntPtr renderer, IntPtr texture, int tm, int tn, uint[] pixels)
		{
			SDL.SDL_Rect textureRect = new SDL.SDL_Rect 
			{ 
				x = 0, 
				y = 0, 
				w = RESX, 
				h = RESY 
			};
			
			int skip = 0;
			for (int m = 0 ; m < tm ; m++)
			{
				for (int n = 0 ; n < tn ; n++)
				{
					int position = skip + n * RESX * RESY;
					int nextPosition = position + RESX * RESY;
					if (nextPosition > pixels.Length) continue;

					int index = position / (RESX * RESY);
					int nextIndex = (nextPosition - 1) / (RESX * RESY);
					
					if (needRefresh[index] || needRefresh[nextIndex])
					{
						fixed (uint* pixelsBuf = &pixels[position])
						{
							SDL.SDL_UpdateTexture(texture, ref textureRect, (IntPtr)pixelsBuf, RESX * sizeof(uint));
						}
						
						SDL.SDL_Rect drawRect = new SDL.SDL_Rect 
						{
							x = m * RESX * zoom, 
							y = n * RESY * zoom, 
							w = RESX * zoom, 
							h = RESY * zoom 
						};
						
						SDL.SDL_RenderCopy(renderer, texture, ref textureRect, ref drawRect);
					}					
				}

				skip += RESX * (winy / zoom);
			}
		}
		
		static uint[] LoadPalette()
		{
			var palette = new uint[256];
			using (var stream = Assembly.GetExecutingAssembly()
				   .GetManifestResourceStream("MemoryViewer.palette.dat"))
			using (BinaryReader br = new BinaryReader(stream))
			{
				var buffer = br.ReadBytes(768);
				for(int i = 0; i < palette.Length; i++)
				{
					byte r = buffer[i * 3 + 0];
					byte g = buffer[i * 3 + 1];
					byte b = buffer[i * 3 + 2];
					palette[i] = (uint)(r << 16 | g << 8 | b);
				}
			}
			
			return palette;
		}
		
		static void SetRefreshState(bool state)
		{
			for(int i = 0 ; i < SCREENS ; i++)
			{
				needRefresh[i] = state;
			}
		}
	}
}