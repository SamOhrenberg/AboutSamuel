<template>
  <div id="main-div">
    <div id="top-content">

        <img src="@/assets/Adobe Express - file-3.png" class="glow-image">
      <div id="top-content-panel-1">
        <h1>Nice to Meet You!</h1>
        <Header>I'm Samuel Ohrenberg</Header>
        <h2>And I'm <span id="emphasize">Fullstack Developer</span> From Oklahoma</h2>
        <h3>I'm a passionate, solution-oriented programmer who loves solving problems. Please speak with my chatbot to learn more about me.</h3>
      </div>
    </div>
    <div id="bottom-content" >
      <div id="content-wrapper-2">
        <div id="item-wrapper">
          <div id="image-wrapper">
            <img id="about-me-image" src="@/assets/photo.jpg">
          </div>
          <div id="about-me-wrapper">
            <header>So, Who Am I?</header>
            <p>Lorem, ipsum dolor sit amet consectetur adipisicing elit. Quia facilis magni voluptate consectetur, voluptatum ullam rerum adipisci eligendi enim explicabo tenetur cupiditate, culpa aperiam in accusamus deserunt amet tempora dicta?</p>
          </div>

        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
  //
</script>

<style>
/* Reset and global styles */
body, html {
  font-family: "Patua One", Cochin, Georgia, Times, serif, Arial, sans-serif;
  box-sizing: border-box;
  height: 100%;
  margin: 0;               /* Remove default margins */
  padding: 0;              /* Remove default paddings */
}

#content-wrapper-2{
display: flex;
width: 75%;
margin: auto;
}
#item-wrapper{
  display: flex;
  flex-direction: row;
  gap: 3rem;
  justify-content: center;
  align-content: center;
}
#about-me-wrapper{
  display: flex;
  flex-direction: column;
}
img{
  max-width: 28rem;
  transform: scaleX(-1);
  height: fit-content;
  align-self: flex-end;
}
#main-div {
  display: flex;
  flex-direction: column;
  height: 100vh;
}
#emphasize{
  color: #fdb831;
}
#top-content {
  display: flex;
  flex-direction: row;
  width: 100%;             /* Take full width */
  height: auto;
  gap: 3rem;               /* Add space between panels */
  background: #001e1e;
  position:relative;
  top: 0;
  justify-content: center;
}
header, h1, h2, h3 {
  padding: 0;
  margin: 0;
}
#top-content-panel-1{
  display: flex;
  flex-direction: column;
  justify-content: center;
  padding: 5rem 0;
}

#top-content-panel-2{
  display: flex;
}
#bottom-content{
  width: 100%;
  flex-grow: 1; /* This makes the bottom content take up the remaining space */
  display: flex;
  flex-direction: column;
  padding: 2rem;
  background-color: #003131;
  padding: 10rem 0;
  z-index: 1;
}

/* Make both panels take equal width */
/* #top-content-panel-1,
#top-content-panel-2 {
  padding: 1rem .5rem;
} */

header {
  color: white;
  font-size: 4rem;
  font-weight: 600;
  padding: .5rem 0;
  margin-top: -15px;
}

h1 {
  margin-top: -10px;
  font-size: 3rem;
  color: #00acac;;
  font-weight: 500;
}

h2 {
  font-size: 2rem;
  padding-bottom: 2rem;
}

h3{

  font-size: 1rem;
  font-weight: 100;
}
/* Glow effect on image */
.glow-image {
  filter: drop-shadow(0 0 15px rgba(0, 255, 255, 0.7)); /* Cyan glow */
  position: relative;
}

#about-me-image{

  padding: 5px;
  border: 1px white solid;
}


</style>
