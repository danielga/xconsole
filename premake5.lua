solution("xconsole")
	language("C#")
	location("")
	warnings("Extra")
	dotnetframework("net6.0")
	defines("TRACE")

	configurations({"Debug", "Release"})

	filter("configurations:Debug")
		defines("DEBUG")
		symbols("On")
		objdir("obj/debug")
		targetdir("bin/debug")

	filter("configurations:Release")
		optimize("On")
		objdir("obj/release")
		targetdir("bin/release")

	project("xconsole")
		kind("WindowedApp")
		files("source/**.cs")
