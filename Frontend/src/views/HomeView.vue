<script setup>
// import { RouterLink } from 'vue-router'
// import HelloWorld from '../components/HelloWorld.vue'
</script>

<template>
  <header class="d-lg-flex flex-row align-space-between">
    <img
      alt="Vue logo"
      class="rounded-circle border-dashed border-xl border-surface"
      src="@/assets/photo.jpg"
      width="300"
      height="300"
    />

    <div id="personal-details">
      <h1>Hello, I am Samuel Ohrenberg</h1>
      <p>
        Lorem ipsum dolor sit amet, consectetur adipisicing elit. Eum illum corporis dolor error
        voluptatem inventore mollitia ratione, placeat voluptates exercitationem optio explicabo hic
        accusantium odit quo aliquam, doloribus labore suscipit officia modi recusandae reiciendis.
      </p>
    </div>
  </header>
</template>

<style scoped>
header {
  height: 300px;
}

img {
  margin-right: 3rem;
}

#personal-details {
  height: 100%;
  overflow: auto;
}
</style>
