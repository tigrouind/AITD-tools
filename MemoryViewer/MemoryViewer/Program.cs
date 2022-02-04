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
		const int RESY = 60;
		const int SCREENS = 40;
		static int winx, winy, zoom;
		static readonly bool[] needRefresh = new bool[SCREENS];
		static bool mustClearScreen;
		static bool needPaletteUpdate;
			
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
			
			uint[] palette = new uint[256];
			byte[] palette256 = new byte[768];
			SetRefreshState(true);

			bool quit = false, mcb = false, minimized = false;
			uint[] pixels = new uint[RESX * RESY * SCREENS];
			uint[] mcbPixels = new uint[RESX * RESY * SCREENS];			
			byte[] pixelData = new byte[RESX * RESY * SCREENS];
			byte[] oldPixelData = new byte[RESX * RESY * SCREENS];			
			long paletteAddress = -1;

			ProcessMemory process = null;
			uint lastCheck = 0, lastCheckPalette = 0;

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
							break;

						case SDL.SDL_EventType.SDL_KEYDOWN:
							switch (sdlEvent.key.keysym.sym)
							{
								case SDL.SDL_Keycode.SDLK_SPACE:
									mcb = !mcb;
									SetRefreshState(true);
									break;
							}
							
							if ((sdlEvent.key.keysym.mod & SDL.SDL_Keymod.KMOD_CTRL) != 0)
							{
								switch (sdlEvent.key.keysym.sym)
								{
									case SDL.SDL_Keycode.SDLK_EQUALS:
									case SDL.SDL_Keycode.SDLK_KP_PLUS:										
										SetZoom(zoom + 1);
										break;
										
									case SDL.SDL_Keycode.SDLK_MINUS:										
									case SDL.SDL_Keycode.SDLK_KP_MINUS:										
										SetZoom(zoom - 1);
										break; 
									
									case SDL.SDL_Keycode.SDLK_0:										
									case SDL.SDL_Keycode.SDLK_KP_0:
										SetZoom(2);
										break; 
								}
							}
							break;

						case SDL.SDL_EventType.SDL_WINDOWEVENT:
							switch(sdlEvent.window.windowEvent)
							{
								case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_RESIZED:
									winx = sdlEvent.window.data1;
									winy = sdlEvent.window.data2;
									SetRefreshState(true);
									break;
									
								case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_MINIMIZED:
									minimized = true;
									break;
									
								case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_RESTORED:
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

						int processId = DosBox.SearchProcess();
						if (processId != -1)
						{
							process = new ProcessMemory(processId);
							process.BaseAddress = process.SearchFor16MRegion();
							if(process.BaseAddress == -1)
							{
								process.Close();
								process = null;
							}
						}
					}
				}

				if (process != null)
				{
					if(paletteAddress == -1)
					{
						uint time = SDL.SDL_GetTicks();
						if ((time - lastCheckPalette) > 1000 || lastCheckPalette == 0)
						{
							lastCheckPalette = time;
							
							//3 bytes + EGA 16 colors palette
							var pattern = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05 };
							paletteAddress = process.SearchForBytePattern(buffer => Tools.IndexOf(buffer, pattern, 1, 4));
						}
					}
				}
				
				if (process != null)
				{
					//DOS conventional memory (640KB)
					//EMS memory (64000B) (skip 64KB (HMA) + 128KB (VCPI))					
					if(!(process.Read(pixelData, 0, 640 * 1024) > 0 &&
						 process.Read(pixelData, (1024+192)*1024, 64000, 640 * 1024) > 0))
					{
						process.Close();
						process = null;
					}
				}

				if (process != null)
				{
					if(paletteAddress != -1)
					{
						if (process.Read(palette256, paletteAddress + 19 - process.BaseAddress, palette256.Length) <= 0) {
							process.Close();
							process = null;
						}								
					}
				}				
				
				UpdatePalette(palette256, palette);
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
				mustClearScreen = false;
				
				if (minimized)
				{
					SDL.SDL_Delay(1);
				}

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
		
		static bool UpdatePalette(byte[] palette256, uint[] palette)
		{
			needPaletteUpdate = false;
			
			int src = 0;
			for(int i = 0 ; i < 256 ; i++)
			{						
				var r = palette256[src++];
				var g = palette256[src++];
				var b = palette256[src++];
				
				uint val = unchecked((uint)(((r << 2) | (r >> 4 )) << 16 | ((g << 2) | (g >> 4)) << 8 | ((b << 2) | (b >> 4)) << 0));				
				if(palette[i] != val)
				{
					palette[i] = val;
					needPaletteUpdate = true;						
				}
			}
			
			return needPaletteUpdate;
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
						if(*pixelsPtr != *oldPixelsPtr || *(pixelsPtr+1) != *(oldPixelsPtr+1) || needPaletteUpdate)
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
					
					SDL.SDL_Rect drawRect = new SDL.SDL_Rect 
					{
						x = m * RESX * zoom, 
						y = n * RESY * zoom, 
						w = RESX * zoom, 
						h = RESY * zoom 
					};
					
					if (nextPosition > pixels.Length)
					{
						if (mustClearScreen)
						{
							SDL.SDL_SetRenderDrawColor(renderer, 0, 0, 0, 255);
							SDL.SDL_RenderFillRect(renderer, ref drawRect);
						}
						continue;
					}

					int index = position / (RESX * RESY);
					int nextIndex = (nextPosition - 1) / (RESX * RESY);
					
					if (needRefresh[index] || needRefresh[nextIndex])
					{
						fixed (uint* pixelsBuf = &pixels[position])
						{
							SDL.SDL_UpdateTexture(texture, ref textureRect, (IntPtr)pixelsBuf, RESX * sizeof(uint));
						}
																	
						SDL.SDL_RenderCopy(renderer, texture, ref textureRect, ref drawRect);
					}					
				}

				skip += RESX * (winy / zoom);
			}
		}
		
		static void SetRefreshState(bool state)
		{
			for(int i = 0 ; i < SCREENS ; i++)
			{
				needRefresh[i] = state;
			}
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
	}
}